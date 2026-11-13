using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.DTOs.Common;
using NEXT.Application.DTOs.Security;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.Features.ApplicationUsers.Queries;
using NEXT.Application.Features.RecycleBin;
using NEXT.Application.Features.Security.Roles;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;
using Xunit;

namespace NEXT.Tests;

public sealed class ProjectionReadTests
{
    [Fact]
    public void RelatedRecordProjectionsTranslateToPostgresAndOnlySelectAllowedFields()
    {
        using var db = new NEXTDbContext(new DbContextOptionsBuilder<NEXTDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=unused;Password=unused").Options, null!, null!);
        var mapping = new TypeAdapterConfig();
        mapping.Scan(typeof(ApplicationRegistrationExtensions).Assembly);
        string Sql<T>() where T : class => db.Set<T>().AsNoTracking().OrderBy(x => EF.Property<Guid>(x, "Id"))
            .Skip(1).Take(10).ProjectToType<RelatedRecordDto>(mapping).ToQueryString();
        foreach (var sql in new[] { Sql<Customer>(), Sql<CustomerContact>(), Sql<Product>(), Sql<Category>(),
                     Sql<Stock>(), Sql<StockMovement>(), Sql<SalesInvoice>(), Sql<Payment>(), Sql<JournalEntry>() })
        {
            Assert.Contains("LIMIT", sql);
            Assert.Contains("OFFSET", sql);
            Assert.DoesNotContain("AvatarUrl", sql);
            Assert.DoesNotContain("PasswordHash", sql);
            Assert.DoesNotContain("RowVersion", sql);
            Assert.DoesNotContain("TaxRate", sql);
        }
    }

    [Fact]
    public async Task RelatedRecordProjectionKeepsRawValuesAndTheGraphReaderUsesIt()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var mapping = scope.ServiceProvider.GetRequiredService<TypeAdapterConfig>();
        var product = new Product
        {
            Name = "Related product",
            CostPrice = 12.5m
        };
        var warehouse = new Warehouse
        {
            Name = "Related warehouse",
            Code = "RELATED"
        };
        db.AddRange(product, warehouse);
        var stock = new Stock
        {
            Product = product,
            Warehouse = warehouse,
            Quantity = 500.25m,
            ReservedQuantity = 2m
        };
        var movement = new StockMovement
        {
            Product = product,
            Warehouse = warehouse,
            Quantity = 1.25m,
            Type = NEXT.Domain.Enums.StockMovementType.TransferOut,
            Notes = null
        };
        db.AddRange(stock, movement);
        await db.SaveChangesAsync();
        var projected = await db.Stocks.AsNoTracking().ProjectToType<RelatedRecordDto>(mapping).SingleAsync();
        Assert.Equal(stock.Id, projected.Id);
        Assert.Equal(500.25m, Assert.IsType<decimal>(projected.Values["Quantity"]));
        Assert.Equal(2m, Assert.IsType<decimal>(projected.Values["ReservedQuantity"]));
        Assert.All(projected.Values.Keys, key => Assert.Contains(key, RelatedRecordDto.FieldNames));
        var projectedMovement = await db.StockMovements.AsNoTracking().ProjectToType<RelatedRecordDto>(mapping).SingleAsync();
        Assert.Equal(movement.Type, Assert.IsType<NEXT.Domain.Enums.StockMovementType>(projectedMovement.Values["Type"]));
        Assert.Null(projectedMovement.Values["Notes"]);
        var reader = scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.Persistence.IRelatedRecordsReader>();
        var activity = await reader.ReadAsync<Product>(product.Id, nameof(Stock), 1, default);
        Assert.Equal(stock.Id, Assert.Single(activity.Records).Id);
        Assert.Equal(500.25m, activity.Records[0].Values["Quantity"]);
    }

    [Fact]
    public void ReadProjectionsTranslateToPostgresAndDoNotSelectCredentials()
    {
        using var db = new NEXTDbContext(new DbContextOptionsBuilder<NEXTDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=unused;Password=unused").Options, null!, null!);
        var mapping = new TypeAdapterConfig();
        mapping.Scan(typeof(ApplicationRegistrationExtensions).Assembly);
        new NEXT.Infrastructure.Identity.IdentityMappingRegister().Register(mapping);
        var profile = db.Users.AsNoTracking().Where(x => x.Id == Guid.NewGuid()).ProjectToType<UserDto>(mapping).ToQueryString();
        var users = db.Users.AsNoTracking().ProjectToType<UserResponseDto>(mapping).ToQueryString();
        var roles = db.Roles.AsNoTracking().ProjectToType<RoleDto>(mapping).ToQueryString();
        var assignments = new NEXT.Infrastructure.Services.RolePermissionService(db).ReadAssignments().ToQueryString();
        var branches = db.Branches.AsNoTracking().ProjectToType<BranchOption>(mapping).ToQueryString();
        foreach (var sql in new[] { profile, users, roles, assignments, branches })
        {
            Assert.Contains("SELECT", sql);
            Assert.DoesNotContain("PasswordHash", sql);
            Assert.DoesNotContain("SecurityStamp", sql);
        }
        Assert.Contains("\"UserRoles\"", profile);
        Assert.Contains("EXISTS", users);
        Assert.DoesNotContain("AvatarUrl", users);
        Assert.Contains("\"Permissions\"", assignments);
        string ReferenceSql<T>() where T : class => db.Set<T>().AsNoTracking().OrderBy(x => EF.Property<Guid>(x, "Id"))
            .ProjectToType<ReferenceOptionDto>(mapping).ToQueryString();
        foreach (var sql in new[] { ReferenceSql<Customer>(), ReferenceSql<Tag>(), ReferenceSql<Category>(),
                     ReferenceSql<Branch>(), ReferenceSql<Warehouse>(), ReferenceSql<Product>(), ReferenceSql<UnitOfMeasure>(), ReferenceSql<Permission>() })
        {
            Assert.Contains("\"Id\"", sql);
            Assert.DoesNotContain("AvatarUrl", sql);
            Assert.DoesNotContain("CostPrice", sql);
        }
        Assert.Contains("\"Module\"", ReferenceSql<Permission>());
        Assert.Contains("\"Code\"", ReferenceSql<Permission>());
        var deleted = db.Products.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedAt != null)
            .OrderByDescending(x => x.DeletedAt).ThenBy(x => x.Id).Skip(1).Take(1)
            .ProjectToType<RecycleBinItemDto<Guid>>(mapping).ToQueryString();
        Assert.Contains("LIMIT", deleted);
        Assert.Contains("OFFSET", deleted);
        Assert.Contains("\"DeletedAt\"", deleted);
        Assert.DoesNotContain("CostPrice", deleted);
        Assert.DoesNotContain("AvatarUrl", deleted);
    }

    [Fact]
    public async Task UserAndRoleQueriesReturnIndependentModelsWhileIdentityRowsStayTracked()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<NEXTDbContext>();
        var id = await db.Users.Where(x => x.UserName == "ui-admin").Select(x => x.Id).SingleAsync();
        var roleId = await db.Roles.Where(x => x.Name == "Admin").Select(x => x.Id).SingleAsync();
        var sender = services.GetRequiredService<ISender>();
        var users = services.GetRequiredService<IUserQueryService>();
        var roles = services.GetRequiredService<IRoleManagementService>();
        var byId = await sender.Send(new GetApplicationUserByIdQuery(id.ToString()));
        var byName = await sender.Send(new GetApplicationUserByNameQuery("UI-ADMIN"));
        var byEmail = await sender.Send(new GetApplicationUserByEmailQuery("UI-ADMIN@EXAMPLE.TEST"));
        foreach (var user in new[] { byId, byName, byEmail })
        {
            Assert.NotNull(user);
            Assert.Equal(id, user.Id);
            Assert.Contains("Admin", user.Roles);
        }
        var list = await users.GetUsersAsync();
        Assert.True(list.Single(x => x.UserName == "ui-admin").HasRole);
        Assert.False(list.Single(x => x.UserName == "ui-reader").HasRole);
        Assert.NotNull(await sender.Send(new GetRoleByIdQuery(roleId)));
        Assert.Contains(await sender.Send(new GetRolesQuery()), x => x.Id == roleId && x.Name == "Admin");
        Assert.Null(await users.FindUserAsync<UserDto>(Guid.NewGuid().ToString()));
        Assert.Null(await users.FindUserByNameAsync<UserDto>("missing-user"));
        Assert.Null(await users.FindUserByEmailAsync<UserDto>("missing@example.test"));
        Assert.Null(await roles.FindByIdAsync<RoleDto>(Guid.NewGuid()));
        Assert.Empty(db.ChangeTracker.Entries());
        var account = Assert.IsType<UserAccount>(await users.FindUserAsync(id.ToString()));
        var role = Assert.IsType<AccessRole>(await roles.FindByIdAsync(roleId));
        var userRow = Assert.Single(db.ChangeTracker.Entries<NEXT.Infrastructure.Identity.Entities.ApplicationUser>());
        var roleRow = Assert.Single(db.ChangeTracker.Entries<NEXT.Infrastructure.Identity.Entities.Role>());
        Assert.Equal(id, account.Id);
        Assert.Equal(roleId, role.Id);
        account.FullName = "Unpersisted profile";
        role.Description = "Unpersisted role";
        Assert.NotEqual(account.FullName, userRow.Entity.FullName);
        Assert.NotEqual(role.Description, roleRow.Entity.Description);
        Assert.Equal(EntityState.Unchanged, userRow.State);
        Assert.Equal(EntityState.Unchanged, roleRow.State);
    }
}
