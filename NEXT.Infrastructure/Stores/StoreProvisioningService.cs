using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NEXT.Application.DTOs.Settings;
using NEXT.Application.DTOs.Stores;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Settings;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Identity.Entities;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Stores;

public sealed class StoreProvisioningService(StoreServerSettings server, StoreCatalog catalog, NEXT.Application.Abstractions.Context.ICurrentStore current,
    IServiceScopeFactory scopes, IEntityValidator<CreateStoreInput> validator, TimeProvider clock,
    ILogger<StoreProvisioningService> logger)
{
    public async Task<StoreRegistration> CreateAsync(CreateStoreInput input, Guid actorId, CancellationToken ct)
    {
        input.Name = input.Name?.Trim() ?? ""; input.OwnerName = input.OwnerName?.Trim() ?? "";
        input.OwnerUserName = input.OwnerUserName?.Trim() ?? ""; input.OwnerEmail = input.OwnerEmail?.Trim() ?? "";
        input.Slug = string.IsNullOrWhiteSpace(input.Slug) ? "store-" + input.OperationId.ToString("N")[..10] : input.Slug.Trim();
        validator.Validate(input);
        if (!current.IsDefault || !server.Enabled || input.OperationId == Guid.Empty || actorId == Guid.Empty) throw new ConflictException("StoreCreationUnavailable");
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { input.Name, input.Slug, input.OwnerName, input.OwnerUserName, input.OwnerEmail })));
        var adminOptions = new NpgsqlConnectionStringBuilder(server.ManagementConnection) { Pooling = false };
        var runtimeOptions = new NpgsqlConnectionStringBuilder(server.RootConnection);
        if (adminOptions.Username != runtimeOptions.Username) throw new ConflictException("StoreProvisioningRoleMismatch");
        await using var management = new NpgsqlConnection(adminOptions.ConnectionString);
        await management.OpenAsync(ct);
        await using (var gate = new NpgsqlCommand("SELECT pg_try_advisory_lock(hashtextextended(@key,0))", management))
        {
            gate.Parameters.AddWithValue("key", "store-provisioning:" + input.OperationId.ToString("N"));
            if (await gate.ExecuteScalarAsync(ct) is not true) throw new ConflictException("StoreProvisioningBusy");
        }
        var store = await catalog.FindAsync(input.OperationId, ct);
        var newlyRegistered = store is null;
        if (store is null)
        {
            store = new(input.OperationId, input.Name, input.Slug, StoreServerSettings.DatabaseName(input.OperationId),
                StoreState.Provisioning, input.OwnerName, input.OwnerUserName, input.OwnerEmail, hash, actorId, clock.GetUtcNow(), 1);
            await catalog.InsertAsync(store, ct);
            store = await catalog.FindAsync(store.Id, ct) ?? throw new ConflictException("StoreChanged");
        }
        if (store.RequestHash != hash) throw new ConflictException("StoreOperationConflict");
        if (store.State == StoreState.Active) return store;
        if (store.State == StoreState.Disabled) throw new ConflictException("StoreCreationUnavailable");
        if (store.State == StoreState.Failed) store = await catalog.ChangeStateAsync(store.Id, StoreState.Provisioning, store.Version, ct);
        try
        {
            _ = server.ConnectionFor(store);
            await using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname=@name)", management);
            exists.Parameters.AddWithValue("name", store.DatabaseName);
            if (await exists.ExecuteScalarAsync(ct) is true)
            {
                if (newlyRegistered) throw new ConflictException("StoreDatabaseAlreadyExists");
            }
            else
            {
                var name = new NpgsqlCommandBuilder().QuoteIdentifier(store.DatabaseName);
                await using var create = new NpgsqlCommand("CREATE DATABASE " + name, management) { CommandTimeout = 120 };
                await create.ExecuteNonQueryAsync(ct);
            }
            await InitializeAsync(store, input.OwnerPassword, ct);
            return await catalog.ChangeStateAsync(store.Id, StoreState.Active, store.Version, ct);
        }
        catch
        {
            // Retain the database and registration for a safe retry. Never remove merchant data.
            try { await catalog.ChangeStateAsync(store.Id, StoreState.Failed, store.Version, CancellationToken.None); }
            catch { logger.LogWarning("Store provisioning state could not be updated for {StoreId}", store.Id); }
            logger.LogWarning("Store provisioning did not complete for {StoreId}; its database was retained", store.Id);
            throw;
        }
    }

    private async Task InitializeAsync(StoreRegistration store, string password, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<StoreContext>().Select(store, provisioning: true);
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        db.Database.SetConnectionString(server.ConnectionFor(store, management: true));
        await db.Database.MigrateAsync(ct);
        db.Database.SetConnectionString(server.ConnectionFor(store));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var branch = await db.Branches.FirstOrDefaultAsync(ct);
        if (branch is null)
        {
            branch = new Branch { Id = Guid.NewGuid(), Name = store.Name, IsActive = true };
            db.Branches.Add(branch);
            await db.SaveChangesAsync(ct);
        }
        if (!await db.Warehouses.AnyAsync(ct))
        {
            db.Warehouses.Add(new Warehouse { Name = "المخزن الرئيسي / Main warehouse", Code = "MAIN", BranchId = branch.Id, IsDefault = true });
            await db.SaveChangesAsync(ct);
        }
        var role = server.OwnerRole;
        if (!await roles.RoleExistsAsync(role)) Require(await roles.CreateAsync(new Role { Id = Guid.NewGuid(), Name = role, IsSystem = true }));
        var owner = await users.FindByNameAsync(store.OwnerUserName);
        if (owner is null)
        {
            owner = new ApplicationUser { Id = Guid.NewGuid(), UserName = store.OwnerUserName, Email = store.OwnerEmail,
                FullName = store.OwnerName, IsActive = true, BranchId = branch.Id, CreatedOn = clock.GetUtcNow() };
            Require(await users.CreateAsync(owner, password));
        }
        else if (!owner.IsActive || !await users.CheckPasswordAsync(owner, password)) throw new ConflictException("StoreOwnerCredentialsConflict");
        if (!await users.IsInRoleAsync(owner, role)) Require(await users.AddToRoleAsync(owner, role));
        var profile = scope.ServiceProvider.GetRequiredService<StoreProfileSettings>();
        if (string.IsNullOrWhiteSpace((await profile.ReadAsync(ct)).Name))
            await profile.SaveAsync(new StoreProfileInput { Name = store.Name }, ct);
    }

    private static void Require(IdentityResult result)
    {
        if (!result.Succeeded) throw new AppValidationException(new Dictionary<string, string[]>
            { ["OwnerPassword"] = result.Errors.Select(e => e.Description).ToArray() });
    }
}
