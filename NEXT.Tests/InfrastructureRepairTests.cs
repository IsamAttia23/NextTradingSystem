using System.Data.Common;
using System.Globalization;
using System.Net;
using ImageMagick;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.ApplicationUsers.Commands;
using NEXT.Application.Features.RecycleBin;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Features.RecycleBin;
using NEXT.Infrastructure.Media;
using NEXT.Infrastructure.Persistence;
using NEXT.Infrastructure.Persistence.Repositories;
using NEXT.Infrastructure.Persistence.Repositories.Entities;
using NEXT.Infrastructure.Persistence.UsersManagement;
using NEXT.web.Services;
using Xunit;

namespace NEXT.Tests;

public sealed class InfrastructureRepairTests
{
    [Fact]
    public async Task NoRoleMarkerDisappearsAfterAssignmentAndCreateWarningIsVisible()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var form = await client.GetStringAsync("/Users/Create");
        Guid branch;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            branch = await scope.ServiceProvider.GetRequiredService<NEXTDbContext>().Branches.Select(x => x.Id).FirstAsync();
        }
        var response = await WorkspaceUiTests.Post(client, "/Users/Create", WorkspaceUiTests.Token(form),
                    ("FullName", "Role warning user"), ("UserName", "no-role-user"), ("Email", "role-warning@example.test"),
                    ("Password", "Test-only!2026"), ("BranchId", branch.ToString()), ("Role", "MissingRole"), ("IsActive", "true"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("warning", await response.Content.ReadAsStringAsync());
        var html = await client.GetStringAsync("/Users");
        Assert.Contains("data-no-role", html);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByNameAsync("no-role-user");
            Assert.NotNull(user);
            var table = await services.GetRequiredService<UsersPresenter>().IndexAsync((_, _) => "/", default);
            Assert.Equal("UserHasNoRole", table.Rows.Single(x => x.Cells[0] == "Role warning user").NameWarning);
            Assert.True((await services.GetRequiredService<ISender>().Send(new AssignUserRoleCommand(user.Id, "Admin"))).Succeeded);
            table = await services.GetRequiredService<UsersPresenter>().IndexAsync((_, _) => "/", default);
            Assert.Null(table.Rows.Single(x => x.Cells[0] == "Role warning user").NameWarning);
        }
    }

    [Fact]
    public async Task ImageAdmissionSupportsCancellationAndConcurrentUploads()
    {
        var processor = new ProfileImageProcessor();
        using var image = new MagickImage(MagickColors.Green, 48, 32);
        var input = image.ToByteArray(MagickFormat.Png);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.NormalizeAsync(input, canceled.Token));
        var outputs = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => processor.NormalizeAsync(input)));
        Assert.All(outputs, output => {
            using var decoded = new MagickImage(output);
            Assert.Equal(48U, decoded.Width);
        });
    }

    [Fact]
    public void RelatedRecordValuesRemainRawAndQuantitiesKeepPlainFormatAcrossCultures()
    {
        var record = new RelatedRecordDto(Guid.NewGuid(), new Dictionary<string, object?>
        {
            ["Quantity"] = 1234.5m,
            ["IsActive"] = false,
            ["Amount"] = 1234.5m
        });
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var english = RelatedRecordPresenter.Fields(record);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var german = RelatedRecordPresenter.Fields(record);
            Assert.Equal("1234.5", english[0].Value);
            Assert.Equal(english[0].Value, german[0].Value);
            Assert.NotEqual(english[2].Value, german[2].Value);
            Assert.IsType<decimal>(record.Values["Quantity"]);
            Assert.Equal("Inactive", german[1].Value);
            Assert.True(german[1].TranslateValue);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [PostgresFact]
    public async Task AuditFailureRollsBackBothWritesAndRestoresTrackerForSyncAndAsyncSaves()
    {
        await using var fixture = await PostgresInventoryTests.Database.Create();
        await fixture.Db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Injected audit failure'; END $$;
            CREATE TRIGGER reject_audit BEFORE INSERT ON "AuditLogs" FOR EACH ROW EXECUTE FUNCTION reject_audit();
            """);
        foreach (var external in new[] { false, true })
        {
            foreach (var sync in new[] { false, true })
            {
                foreach (var accept in new[] { false, true })
                {
                    await using var db = fixture.NewContext();
                    await using var transaction = external ? await db.Database.BeginTransactionAsync() : null;
                    var product = new Product
                    {
                        Name = $"Rejected-{external}-{sync}-{accept}"
                    };
                    db.Add(product);
                    if (sync)
                    {
                        Assert.Throws<DbUpdateException>(() => db.SaveChanges(accept));
                    }
                    else
                    {
                        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(accept));
                    }
                    Assert.Equal(EntityState.Added, db.Entry(product).State);
                    Assert.Empty(product.RowVersion);
                    Assert.Empty(db.ChangeTracker.Entries<AuditLog>());
                    if (transaction is not null)
                    {
                        await transaction.CommitAsync(); // A handled failure cannot leave half the save committed.
                    }
                    await using var verification = fixture.NewContext();
                    Assert.False(await verification.Products.AnyAsync(x => x.Id == product.Id));
                    Assert.False(await verification.AuditLogs.AnyAsync(x => x.EntityId == product.Id));
                }
            }
        }
    }

    [PostgresFact]
    public async Task SaveChangesFalseWritesEachEntityOnceAndKeepsPendingState()
    {
        await using var fixture = await PostgresInventoryTests.Database.Create();
        foreach (var sync in new[] { false, true })
        {
            await using var db = fixture.NewContext();
            var product = new Product
            {
                Name = "Save false " + sync
            };
            db.Add(product);
            var affected = sync ? db.SaveChanges(false) : await db.SaveChangesAsync(false);
            Assert.Equal(2, affected);
            Assert.Equal(EntityState.Added, db.Entry(product).State);
            Assert.NotEmpty(product.RowVersion);
            await using var verification = fixture.NewContext();
            Assert.Equal(1, await verification.Products.CountAsync(x => x.Id == product.Id));
            Assert.Equal(1, await verification.AuditLogs.CountAsync(x => x.EntityId == product.Id));
            db.ChangeTracker.AcceptAllChanges();
            Assert.Equal(0, await db.SaveChangesAsync());
        }
    }

    [PostgresFact]
    public async Task HandledNestedFailurePreservesEarlierOuterChanges()
    {
        await using var fixture = await PostgresInventoryTests.Database.Create();
        var unit = new UnitOfWork(fixture.Db);
        var branch = await fixture.Db.Branches.SingleAsync(x => x.Id == fixture.Branch);
        await unit.ExecuteInTransactionAsync(async ct =>
        {
            branch.Name = "Outer survives";
            await Assert.ThrowsAsync<InvalidOperationException>(() => unit.ExecuteInTransactionAsync(async token =>
            {
                fixture.Db.Add(new Tag
                {
                    Name = "Nested rollback",
                    Color = "red"
                });
                await fixture.Db.SaveChangesAsync(token);
                throw new InvalidOperationException("Inner failure");
            }, ct));
            Assert.Equal("Outer survives", branch.Name);
        });
        await using var verification = fixture.NewContext();
        Assert.Equal("Outer survives", (await verification.Branches.SingleAsync(x => x.Id == branch.Id)).Name);
        Assert.False(await verification.Set<Tag>().AnyAsync(x => x.Name == "Nested rollback"));
    }

    [PostgresFact]
    public async Task RollbackAndLoggerFailuresPreserveTheOriginalError()
    {
        await using var fixture = await PostgresInventoryTests.Database.Create();
        await using var db = fixture.NewContext(new FailedRollback());
        var unit = new UnitOfWork(db, new FailedLogger());
        var original = new InvalidOperationException("Original operation failure");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => unit.ExecuteInTransactionAsync(_ => Task.FromException(original)));
        Assert.Same(original, error);
        Assert.IsType<IOException>(error.Data["RollbackFailure"]);
        Assert.IsType<IOException>(error.Data["LoggingFailure"]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task EmailIsReservedForSoftDeletedUsersAndReleasedByPhysicalDeletion()
    {
        await using var fixture = await PostgresInventoryTests.Database.Create();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: fixture.TestConnection);
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<NEXTDbContext>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "reserved",
            Email = "reserved@example.test",
            FullName = "Reserved",
            IsActive = true,
            BranchId = fixture.Branch
        };
        Assert.True((await manager.CreateAsync(user, "Test-only!2026")).Succeeded);
        var reader = services.GetRequiredService<IUserQueryService>();
        var commands = services.GetRequiredService<IUserCommandService>();
        Assert.True((await commands.DeleteUserAsync(user)).Succeeded);
        Assert.True(await reader.IsEmailTakenAsync("reserved@example.test"));
        var duplicate = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "different",
            Email = user.Email,
            FullName = "Duplicate",
            IsActive = true,
            BranchId = fixture.Branch
        };
        await Assert.ThrowsAsync<ConflictException>(() => manager.CreateAsync(duplicate, "Test-only!2026"));
        db.ChangeTracker.Clear();
        // This is a fixture-owned database; verify release after actual row removal without changing production deletion policy.
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM security.\"Users\" WHERE \"Id\" = {user.Id}");
        Assert.False(await reader.IsEmailTakenAsync("reserved@example.test"));
        Assert.True((await manager.CreateAsync(duplicate, "Test-only!2026")).Succeeded);
    }

    [PostgresFact]
    public async Task ConcurrentEmailChangesCannotCreateDuplicates()
    {
        await using var fixture = await PostgresInventoryTests.Database.Create();
        var users = Enumerable.Range(0, 2).Select(i => new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "race" + i,
            NormalizedUserName = "RACE" + i,
            Email = $"race{i}@example.test",
            NormalizedEmail = $"RACE{i}@EXAMPLE.TEST",
            FullName = "Race",
            BranchId = fixture.Branch
        }).ToArray();
        fixture.Db.AddRange(users);
        await fixture.Db.SaveChangesAsync();
        async Task<bool> Change(Guid id)
        {
            await using var db = fixture.NewContext();
            var user = await db.Users.SingleAsync(x => x.Id == id);
            user.Email = "shared@example.test";
            user.NormalizedEmail = "SHARED@EXAMPLE.TEST";
            try
            {
                await db.SaveChangesAsync();
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        }
        var results = await Task.WhenAll(users.Select(x => Change(x.Id)));
        Assert.Single(results, x => x);
        Assert.Equal(1, await fixture.Db.Users.CountAsync(x => x.NormalizedEmail == "SHARED@EXAMPLE.TEST"));
    }

    [PostgresFact]
    public async Task MigrationsCreateFreshAndPreviouslySqlUpgradedDatabases()
    {
        foreach (var upgraded in new[] { false, true })
        {
            await using var fixture = await PostgresInventoryTests.Database.Create(migrate: true, preupgraded: upgraded);
            Assert.False(fixture.Db.Database.HasPendingModelChanges());
            Assert.Empty(await fixture.Db.Database.GetPendingMigrationsAsync());
            Assert.True(await fixture.Db.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'EmailIndex' AND indexdef LIKE 'CREATE UNIQUE INDEX%') AS \"Value\"").SingleAsync());
            var tag = new Tag
            {
                Name = "Migration tag",
                Color = "blue"
            };
            fixture.Db.Add(tag);
            var supplier = new Supplier
            {
                Name = "Migrated supplier",
                AvatarUrl = [1, 2, 3],
                Tag = tag
            };
            fixture.Db.Add(supplier);
            await fixture.Db.SaveChangesAsync();
            Assert.Equal(new byte[] { 1, 2, 3 }, (await fixture.Db.Suppliers.AsNoTracking().SingleAsync()).AvatarUrl);
        }
    }

    [PostgresFact]
    public async Task IdentityUniquenessChecksUseExistsWithoutLoadingCredentialsOrImages()
    {
        await using var fixture = await PostgresInventoryTests.Database.Create();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: fixture.TestConnection);
        var counter = new Commands();
        await using var db = fixture.NewContext(counter);
        var store = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<ApplicationUser, Role, NEXTDbContext, Guid, IdentityUserClaim<Guid>, UserRole, IdentityUserLogin<Guid>, IdentityUserToken<Guid>, IdentityRoleClaim<Guid>>(db);
        var manager = new UserManager<ApplicationUser>(store, Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(), [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), app.Services, Microsoft.Extensions.Logging.Abstractions.NullLogger<UserManager<ApplicationUser>>.Instance);
        await using var scope = app.Services.CreateAsyncScope();
        var queries = new UserQueryService(manager, new Mapster.TypeAdapterConfig(),
            scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.Context.ICurrentUserService>());
        Assert.True(await queries.IsUserNameTakenAsync("ui-admin"));
        Assert.True(await queries.IsEmailTakenAsync("ui-admin@example.test"));
        Assert.Equal(2, counter.Sql.Count);
        Assert.All(counter.Sql, sql => {
            Assert.Contains("EXISTS", sql);
            Assert.DoesNotContain("AvatarUrl", sql);
            Assert.DoesNotContain("PasswordHash", sql);
        });
    }

    [PostgresFact]
    public async Task RecursiveRelatedReadHasBoundedRoundTripsAndProjectsOnlyContractColumns()
    {
        await using var fixture = await PostgresInventoryTests.Database.Create();
        Guid? parent = null;
        var ids = new List<Guid>();
        for (var index = 0; index < 60; index++)
        {
            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = "Tree " + index,
                ParentId = parent
            };
            parent = category.Id;
            ids.Add(category.Id);
            fixture.Db.Add(category);
        }
        await fixture.Db.SaveChangesAsync();
        var counter = new Commands();
        await using var db = fixture.NewContext(counter);
        var mapping = new Mapster.TypeAdapterConfig();
        mapping.Scan(typeof(NEXT.Application.ApplicationRegistrationExtensions).Assembly);
        var result = await new RelatedRecordsReader(db, mapping).ReadAsync<Category>(ids[0], nameof(Category), 1, default);
        Assert.Equal(59, result.TotalCount);
        Assert.Equal(10, result.Records.Count);
        Assert.Contains(counter.Sql, sql => sql.Contains("WITH RECURSIVE"));
        Assert.True(counter.Sql.Count < 15, $"Unexpected round trips: {counter.Sql.Count}");
        Assert.DoesNotContain(counter.Sql, sql => sql.Contains("AvatarUrl"));
    }

    [PostgresFact]
    public async Task RecycleHandlersShareOneTransactionAndRollBackTheirWrites()
    {
        await using var fixture = await PostgresInventoryTests.Database.Create();
        var tag = new Tag
        {
            Name = "Deleted hook",
            Color = "red"
        };
        fixture.Db.Add(tag);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.Remove(tag);
        await fixture.Db.SaveChangesAsync();
        var mapping = new Mapster.TypeAdapterConfig();
        mapping.Scan(typeof(NEXT.Application.ApplicationRegistrationExtensions).Assembly);
        var service = new RecycleBinService<Tag, Guid>(new RecycleBinStore<Tag, Guid>(fixture.Db, new PaginationServices(), mapping),
            new UnitOfWork(fixture.Db), [new FailedRestore(fixture.Db)]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Restore(tag.Id));
        await using var verification = fixture.NewContext();
        Assert.False(await verification.Set<Tag>().AnyAsync(x => x.Id == tag.Id));
        Assert.False(await verification.Branches.AnyAsync(x => x.Name == "Hook write"));
    }

    [PostgresFact]
    public async Task AuditCancellationRollsBackWithoutConvertingCancellationToServerFailure()
    {
        await using var fixture = await PostgresInventoryTests.Database.Create();
        using var cancellation = new CancellationTokenSource();
        await using var db = fixture.NewContext(new CancelAudit(cancellation));
        var product = new Product
        {
            Name = "Canceled audit"
        };
        db.Add(product);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.SaveChangesAsync(cancellation.Token));
        Assert.Equal(EntityState.Added, db.Entry(product).State);
        await using var verification = fixture.NewContext();
        Assert.False(await verification.Products.AnyAsync(x => x.Id == product.Id));
        Assert.False(await verification.AuditLogs.AnyAsync(x => x.EntityId == product.Id));
    }

    [PostgresFact]
    public async Task QueryTimeoutsAreTranslatedAndRetainTheirOriginalCause()
    {
        await using var fixture = await PostgresInventoryTests.Database.Create();
        await using var db = fixture.NewContext(new TimeoutReads());
        var error = await Assert.ThrowsAsync<DependencyUnavailableException>(() => new QueryExecutor().FirstOrDefaultAsync(db.Products));
        Assert.NotNull(error.InnerException);
        var cause = error.InnerException;
        while (cause.InnerException is not null)
        {
            cause = cause.InnerException;
        }
        Assert.IsType<TimeoutException>(cause);
        await Assert.ThrowsAsync<DependencyUnavailableException>(() => new ProductRepository(db).CountAsync());
    }

    [PostgresFact]
    public async Task ProductSortFieldsPreserveDirectionAndTieBreakers()
    {
        await using var fixture = await PostgresInventoryTests.Database.Create();
        var products = Enumerable.Range(1, 4).Select(index => new Product
        {
            Id = Guid.Parse($"00000000-0000-0000-0000-{index:000000000000}"),
            Name = "Sort item",
            Barcode = "SORT" + index,
            CostPrice = index,
            RetailPrice = index,
            WholesalePrice = index,
            VipPrice = index
        }).ToArray();
        fixture.Db.AddRange(products);
        await fixture.Db.SaveChangesAsync();
        var repository = new ProductRepository(fixture.Db);
        var source = repository.FindAsync(x => x.Name == "Sort item");
        var ascending = products.Select(x => x.Id).ToArray();
        var descending = ascending.Reverse().ToArray();
        foreach (var field in new[] { "Name", "Barcode", "CostPrice", "RetailPrice", "WholesalePrice", "VipPrice" })
        {
            Assert.Equal(ascending, await repository.ApplySorting(source, field, NEXT.Domain.Enums.SortDirection.Ascending).Select(x => x.Id).ToArrayAsync());
            Assert.Equal(descending, await repository.ApplySorting(source, field, NEXT.Domain.Enums.SortDirection.Descending).Select(x => x.Id).ToArrayAsync());
        }
        // Preserve the legacy default for an unsupported field, including its ascending fallback.
        Assert.Equal(ascending, await repository.ApplySorting(source, "NotAField", NEXT.Domain.Enums.SortDirection.Descending).Select(x => x.Id).ToArrayAsync());
    }

    [PostgresFact]
    public async Task AuditFailureRestoresTemporaryGeneratedKeysAndCanBeRetriedExplicitly()
    {
        await using var fixture = await PostgresInventoryTests.Database.Create();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "temporary-key",
            FullName = "Temporary key",
            BranchId = fixture.Branch
        };
        fixture.Db.Add(user);
        await fixture.Db.SaveChangesAsync();
        await fixture.Db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Injected audit failure'; END $$;
            CREATE TRIGGER reject_audit BEFORE INSERT ON "AuditLogs" FOR EACH ROW EXECUTE FUNCTION reject_audit();
            """);
        var claim = new IdentityUserClaim<Guid>
        {
            UserId = user.Id,
            ClaimType = "fixture",
            ClaimValue = "value"
        };
        var product = new Product
        {
            Name = "Generated key rollback"
        };
        fixture.Db.AddRange(claim, product);
        Assert.True(fixture.Db.Entry(claim).Property(x => x.Id).IsTemporary);
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
        Assert.True(fixture.Db.Entry(claim).Property(x => x.Id).IsTemporary);
        await fixture.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_audit ON \"AuditLogs\"");
        await fixture.Db.SaveChangesAsync();
        Assert.True(claim.Id > 0);
        await using var verification = fixture.NewContext();
        Assert.Equal(1, await verification.Set<IdentityUserClaim<Guid>>().CountAsync(x => x.UserId == user.Id));
        Assert.Equal(1, await verification.Products.CountAsync(x => x.Id == product.Id));
    }

    private sealed class CancelAudit(CancellationTokenSource cancellation) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO \"AuditLogs\""))
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class TimeoutReads : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) => throw new TimeoutException("Injected query timeout");
    }
    private sealed class FailedRestore(NEXTDbContext db) : IRecycleBinHandler<Tag, Guid>
    {
        public string EntityKey => nameof(Tag);
        public async Task OnRestoreAsync(Tag entity, CancellationToken ct = default)
        {
            db.Add(new Branch
            {
                Name = "Hook write"
            });
            await db.SaveChangesAsync(ct);
            throw new InvalidOperationException("Hook failure");
        }
        public Task OnHardDeleteAsync(Tag entity, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class FailedRollback : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionRollingBackAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) => throw new IOException("Rollback failure");
    }
    private sealed class FailedLogger : ILogger<UnitOfWork>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => throw new IOException("Logger failure");
    }
    private sealed class Commands : DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Sql.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
