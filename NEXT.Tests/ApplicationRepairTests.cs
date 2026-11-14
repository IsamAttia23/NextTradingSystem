using NEXT.Infrastructure.Identity.Entities;
using System.Data.Common;
using System.Net;
using System.Text.RegularExpressions;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Common;
using NEXT.Application.DTOs.Customers;
using NEXT.Application.DTOs.Inventory;
using NEXT.Application.DTOs.Products;
using NEXT.Application.DTOs.Tags;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.ApplicationUsers.Commands;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Features.Dashboard;
using NEXT.Application.Features.Products;
using NEXT.Application.QueryModels;
using NEXT.Application.Services.ReferenceData;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;
using NEXT.Infrastructure.Persistence.Repositories;
using NEXT.Infrastructure.Persistence.Repositories.Entities;
using NEXT.Infrastructure.Persistence.UsersManagement;
using Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NEXT.web.Middleware;
using NEXT.Application.DTOs.Security;

namespace NEXT.Tests;

public sealed class ApplicationRepairTests
{
    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(422)]
    [InlineData(503)]
    [InlineData(500)]
    public async Task WebMapsApplicationErrorCodesAndHidesUnexpectedDetails(int status)
    {
        Exception failure = status switch
        {
            400 => new AppValidationException(new Dictionary<string, string[]>
            {
                ["Name"] = ["Required"]
            }),
            404 => new NotFoundException("Record not found."),
            409 => new ConflictException("Record changed."),
            422 => new UnprocessableEntityException("Record is in use."),
            503 => new DependencyUnavailableException("private-storage-detail"),
            _ => new InvalidOperationException("private-storage-detail")
        };
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => throw failure, NullLogger<ExceptionHandlingMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(status, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("traceId", json);
        if (status >= 500)
        {
            Assert.DoesNotContain("private-storage-detail", json);
        }
    }

    [Fact]
    public async Task RequestCancellationIsNotConvertedToServerFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DefaultHttpContext
        {
            RequestAborted = cancellation.Token
        };
        var middleware = new ExceptionHandlingMiddleware(_ => throw new OperationCanceledException(cancellation.Token),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => middleware.InvokeAsync(context));
    }

    [Fact]
    public async Task UpdateEnvelopeIdentityWinsAndUnknownRolesAreValidated()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var created = await sender.Send(new CreateEntityCommand<PermissionDto, PermissionCreationDto>(
            new()
            {
                Code = "Envelope.Read",
                Module = "Envelope"
            }));
        var unrelatedId = Guid.NewGuid();
        var updated = await sender.Send(new UpdateEntityCommand<PermissionDto, PermissionUpdateDto>(created.Id,
            new()
            {
                Id = unrelatedId,
                Code = "Envelope.Update",
                Module = "Envelope"
            }));
        Assert.Equal(created.Id, updated.Id);
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        Assert.False(await db.Permissions.AnyAsync(x => x.Id == unrelatedId));
        var userId = await db.Users.Where(x => x.UserName == "ui-admin").Select(x => x.Id).SingleAsync();
        await Assert.ThrowsAsync<AppValidationException>(() => sender.Send(new AssignUserRoleCommand(userId, "Missing-role")));
    }

    [Fact]
    public async Task DirectApplicationCallsValidateQueryRangesIdentifiersAndCancellation()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var sender = services.GetRequiredService<ISender>();
        Assert.Empty((await sender.Send(new GetEntitiesQuery<CustomerDto>(
            new CustomerQueryModel
            {
                MinCreditLimit = 100,
                MaxCreditLimit = 50
            }))).Data);
        var inventory = services.GetRequiredService<IInventoryQueryService>();
        Assert.Empty((await inventory.MovementsAsync(new StockMovementQueryModel
        {
            From = DateTimeOffset.UtcNow,
            To = DateTimeOffset.UtcNow.AddDays(-1)
        })).Data);
        var validator = services.GetRequiredService<IEntityValidator<ReceiveStockDto>>();
        var error = Assert.Throws<AppValidationException>(() => validator.Validate(new ReceiveStockDto
        {
            ProductId = Guid.Empty,
            WarehouseId = Guid.NewGuid(),
            Quantity = 1
        }));
        Assert.Contains(nameof(ReceiveStockDto.ProductId), error.Errors.Keys);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sender.Send(new GetEntitiesQuery<TagDto>(new()), cancelled.Token));
    }

    [Fact]
    public void ApplicationFilesHaveMatchingNamesAndOneTopLevelType()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../NEXT.Application"));
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(x => !x.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                        !x.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)))
        {
            var declarations = Regex.Matches(File.ReadAllText(file),
                @"^[ \t]*(?:public|internal)\s+(?:(?:sealed|abstract|static|partial|readonly)\s+)*(?:class|record|interface|enum)\s+(\w+)", RegexOptions.Multiline);
            Assert.True(declarations.Count == 1, $"{file}: expected one top-level application type.");
            Assert.Equal(Path.GetFileNameWithoutExtension(file), declarations[0].Groups[1].Value);
        }
    }

    [Fact]
    public async Task ConflictingCqrsRegistrationsFailAndHostMappingsAreIndependent()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        var services = new ServiceCollection();
        services.AddEntityCqrs<Tag, TagDto, TagInputDto, TagInputDto>();
        Assert.Throws<InvalidOperationException>(() => services.AddEntityCqrs<Product, TagDto, TagInputDto, TagInputDto>());
        var first = new ServiceCollection();
        var second = new ServiceCollection();
        var config = app.Configuration;
        first.AddApplicationServices(config);
        second.AddApplicationServices(config);
        using var firstProvider = first.BuildServiceProvider();
        using var secondProvider = second.BuildServiceProvider();
        Assert.NotSame(firstProvider.GetRequiredService<TypeAdapterConfig>(),
            secondProvider.GetRequiredService<TypeAdapterConfig>());
    }

    [PostgresFact]
    public async Task IdentityCreationKeepsSavedUserWhenRoleAssignmentFails()
    {
        foreach (var scenario in new[] { "result", "exception", "nested" })
        {
            await using var database = await PostgresInventoryTests.Database.Create();
            await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: database.TestConnection,
                configureServices: services => services.AddScoped<IUserCommandService>(provider =>
                    new FailingRoleService(new UserCommandService(provider.GetRequiredService<UserManager<ApplicationUser>>(),
                        provider.GetRequiredService<IUserManagementService>()), scenario == "exception")));
            await using var scope = app.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<NEXTDbContext>();
            var branch = await db.Branches.Where(x => x.IsActive).Select(x => x.Id).FirstAsync();
            var request = new CreateApplicationUserCommand(new()
            {
                FullName = "Atomic user",
                UserName = "atomic-" + scenario,
                Email = scenario + "@atomic.test",
                BranchId = branch,
                Password = "Test-only!2026",
                Role = "Admin"
            });
            var sender = services.GetRequiredService<ISender>();
            var result = scenario == "nested"
                ? await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct => sender.Send(request, ct))
                : await sender.Send(request);
            Assert.True(result.Succeeded);
            Assert.Contains(result.Warnings, x => x.Code == "RoleAssignmentFailed");
            db.ChangeTracker.Clear();
            var saved = await db.Users.SingleAsync(x => x.UserName == request.User.UserName);
            Assert.False(await db.UserRoles.AnyAsync(x => x.UserId == saved.Id));
        }
    }

    [PostgresFact]
    public async Task IndependentHttpEditsRejectStaleAndMissingVersions()
    {
        await using var database = await PostgresInventoryTests.Database.Create();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: database.TestConnection);
        using var first = WorkspaceUiTests.Client(app);
        using var second = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(first, "ui-admin");
        await WorkspaceUiTests.Login(second, "ui-admin");
        var url = "/Products/Edit/" + database.Product;
        var firstForm = await first.GetStringAsync(url);
        var staleForm = await second.GetStringAsync(url);
        var expected = WorkspaceUiTests.RowVersion(staleForm);
        Assert.NotEmpty(expected);
        (string, string)[] prices = [("CostPrice", "0"), ("RetailPrice", "0"), ("WholesalePrice", "0"), ("VipPrice", "0")];
        Assert.Equal(HttpStatusCode.OK, (await WorkspaceUiTests.Post(first, url, WorkspaceUiTests.Token(firstForm),
            [("Name", "First edit"), ("RowVersion", WorkspaceUiTests.RowVersion(firstForm)), ..prices])).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await WorkspaceUiTests.Post(second, url, WorkspaceUiTests.Token(staleForm),
            [("Name", "Stale edit"), ("RowVersion", expected), ..prices])).StatusCode);
        using var missing = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = WorkspaceUiTests.Token(staleForm),
            ["Name"] = "Missing version",
            ["CostPrice"] = "0",
            ["RetailPrice"] = "0",
            ["WholesalePrice"] = "0",
            ["VipPrice"] = "0"
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await second.PostAsync(url, missing)).StatusCode);
        await using var verification = database.NewContext();
        Assert.Equal("First edit", (await verification.Products.SingleAsync(x => x.Id == database.Product)).Name);
    }

    [PostgresFact]
    public async Task SingleRecordAndReferencesUseOneQueryAndDashboardOnlyCounts()
    {
        await using var database = await PostgresInventoryTests.Database.Create();
        var counter = new QueryCounter();
        await using var db = database.NewContext(counter);
        var products = new ProductRepository(db);
        var executor = new QueryExecutor();
        var rateReader = new NEXT.Application.Services.Currencies.ExchangeRateReader(new SystemSettingRepository(db));
        db.Add(new SystemSetting
        {
            Key = NEXT.Application.Services.Currencies.ExchangeRateReader.SettingKey,
            ValueJson = "1"
        });
        await db.SaveChangesAsync();
        counter.Reads = 0;
        var config = new TypeAdapterConfig();
        var handler = new ProductQueries(products, executor, new PaginationServices(),
            new DataAnnotationsEntityValidator<PaginationQuery>(), new ProductPricing(rateReader, config), config);
        Assert.Equal(database.Product, (await handler.Handle(new GetEntityByIdQuery<ProductDto>(database.Product), default)).Id);
        Assert.Equal(2, counter.Reads);
        counter.Reads = 0;
        var mapping = new TypeAdapterConfig();
        mapping.Scan(typeof(ApplicationRegistrationExtensions).Assembly);
        var source = new ReferenceSource<Product>("Products", products, executor, mapping);
        Assert.Single(await source.GetAsync(default));
        Assert.Equal(1, counter.Reads);
        counter.Reads = 0;
        counter.Commands.Clear();
        var dashboard = new GetDashboardHandler(new NEXT.Infrastructure.Identity.DashboardAccountCounts(db),
            new TRepository<RolePermission>(db), new TRepository<Permission>(db));
        await dashboard.Handle(new(), default);
        Assert.Equal(5, counter.Reads);
        Assert.All(counter.Commands, sql => Assert.Contains("count(", sql.ToLowerInvariant()));
    }

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Reads { get; set; }
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            Reads++;
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FailingRoleService(IUserCommandService inner, bool throwError) : IUserCommandService
    {
        public Task<UserOperationResult> CreateUserAsync(UserAccount user, string password, CancellationToken ct = default) => inner.CreateUserAsync(user, password, ct);
        public Task<UserOperationResult> UpdateUserAsync(UserAccount user, CancellationToken ct = default) => inner.UpdateUserAsync(user, ct);
        public Task<UserOperationResult> DeleteUserAsync(UserAccount user, CancellationToken ct = default) => inner.DeleteUserAsync(user, ct);
        public Task<UserOperationResult> ChangePasswordAsync(UserAccount user, string currentPassword, string newPassword, CancellationToken ct = default) => inner.ChangePasswordAsync(user, currentPassword, newPassword, ct);
        public Task<UserOperationResult> SetUserNameAsync(UserAccount user, string userName, CancellationToken ct = default) => inner.SetUserNameAsync(user, userName, ct);
        public Task<UserOperationResult> SetUserEmailAsync(UserAccount user, string email, CancellationToken ct = default) => inner.SetUserEmailAsync(user, email, ct);
        public Task<UserOperationResult> SetPhoneNumberAsync(UserAccount user, string phoneNumber, CancellationToken ct = default) => inner.SetPhoneNumberAsync(user, phoneNumber, ct);
        public Task<UserOperationResult> AddUserToRole(UserAccount user, string role, CancellationToken ct = default) => throwError
            ? throw new InvalidOperationException("Injected role storage failure.")
            : Task.FromResult(UserOperationResult.Failure([new("InjectedRoleFailure", "Role assignment failed.")]));
        public Task<UserOperationResult> AddUserToRoles(UserAccount user, IReadOnlyList<string> roles, CancellationToken ct = default) =>
            AddUserToRole(user, roles[0], ct);
    }
}
