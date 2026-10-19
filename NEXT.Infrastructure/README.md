# NEXT.Infrastructure

Infrastructure implementation for NEXT Trading System (.NET 10 / EF Core 10 / PostgreSQL).

## Persistence and repositories

- `NEXTDbContext` is the EF Core context and Identity store context.
- `IRepository<TEntity, TKey>` is declared in `NEXT.Application` so application use cases depend on abstractions rather than EF Core.
- `Repository<TEntity, TKey>` is the single generic EF Core implementation. It is registered as an open generic, so it works with all mapped entities without creating one repository class per entity.
- `IUnitOfWork` exposes `Repository<TEntity, TKey>()`, `SaveChangesAsync`, and transaction execution. Repositories share the same scoped `NEXTDbContext`.
- Typical entities use `long` keys: `unitOfWork.Repository<Product, long>()`.
- `SystemSetting` uses a string key: `unitOfWork.Repository<SystemSetting, string>()`.
- For composite keys such as `RolePermission` and `UserRole`, use `GetByKeyAsync([roleId, permissionId])`. `GetByIdAsync` also accepts an `object[]` as the key argument when `TKey` is `object[]`.
- `Query()` returns `IQueryable<TEntity>` so application queries can compose filtering, ordering, paging, and `Include` before materialization. Keep business rules in Application/Domain, not in repositories.

## Included

- `NEXTDbContext` based on ASP.NET Core Identity with `long` keys.
- Explicit table names, relationship mappings, delete behaviors, unique indexes, precision for monetary/quantity fields, and PostgreSQL `jsonb` columns.
- Identity stores, role management, token providers, password policy, and lockout settings registered through `AddInfrastructure`.
- `ICurrentUserService` and `IRequestMetadataService` for current user, IP address, and User-Agent.
- Automatic audit rows for tracked added/updated/deleted/restored entities, excluding audit and recycle-bin records themselves. Sensitive password/token properties are omitted from snapshots.
- Soft-delete conversion for `SoftDeletableEntity` and `AppUser`, with JSON snapshot written to `RecycleBinEntry`. Global query filters hide soft-deleted entities by default.
- `NEXTDbContextFactory` for EF Core tooling.

## Registration

`NEXT.Api` calls `builder.Services.AddInfrastructure(builder.Configuration)`. This registers the DbContext, Identity, generic repositories, and Unit of Work. Do not register a repository class per entity.

## Configuration and migrations

Set `ConnectionStrings:NEXTDatabase` in `src/NEXT.Api/appsettings.json` or use `ConnectionStrings__NEXTDatabase`. Do not commit production credentials.

```bash
dotnet restore NEXTTradingSystem.slnx
dotnet build NEXTTradingSystem.slnx
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project src/NEXT.Infrastructure --startup-project src/NEXT.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/NEXT.Infrastructure --startup-project src/NEXT.Api
```

## Boundaries and caveats

- Infrastructure records recycle-bin snapshots and soft-deletes entities, but restore/purge authorization, expiry cleanup, image/file relocation, and entity-specific restoration belong in application use cases.
- `ReferenceType`/`ReferenceId` pairs are polymorphic references, not database foreign keys; validate them in application logic.
- Financial posting, stock movement, invoice confirmation, and balance updates must be implemented in application/domain services and saved transactionally.
- `Query()` exposes `IQueryable`; use it deliberately so filtering and paging run in the database. Avoid returning it across API boundaries.
- The project was not compiled in the generation environment because the .NET SDK is unavailable. Run `dotnet build NEXTTradingSystem.slnx` locally and resolve any SDK/package-specific diagnostics before production use.


## Persistence code organization

- `Persistence/Configurations/` contains one `IEntityTypeConfiguration<T>` file per domain/Identity model. `NEXTDbContext` loads them with `ApplyConfigurationsFromAssembly`.
- `Persistence/Repositories/Repository.cs` is the single generic EF Core repository implementation used for all entity types.
- `Persistence/Repositories/UnitOfWork.cs` coordinates repository resolution, saving, and transactions.

This intentionally avoids creating near-identical repository classes for every entity; add a dedicated repository only when an entity needs genuinely specialized queries.
