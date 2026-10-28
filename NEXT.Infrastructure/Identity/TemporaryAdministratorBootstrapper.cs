using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Identity;

/// <summary>Explicit, Development-only bootstrap. Credentials are supplied outside source control.</summary>
public sealed class TemporaryAdministratorBootstrapper(NEXTDbContext db, UserManager<ApplicationUser> users,
    RoleManager<Role> roles, IHostEnvironment environment, IConfiguration configuration)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("DevelopmentAccess:Enabled")) return;
        var name = configuration["DevelopmentAccess:UserName"];
        var password = configuration["DevelopmentAccess:Password"];
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("DevelopmentAccess credentials must be supplied through local configuration.");
        var existing = await users.FindByNameAsync(name);
        if (existing is not null)
        {
            if (!(await users.GetClaimsAsync(existing)).Any(x => x.Type == LoginAccessPolicy.TemporaryClaim))
                throw new InvalidOperationException("The temporary username is already used by a regular account.");
            return; // Never reset an existing password on startup.
        }
        if (await roles.RoleExistsAsync("Admin") && (await users.GetUsersInRoleAsync("Admin")).Any(x => x.IsActive))
            return;
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var branch = await db.Branches.FirstOrDefaultAsync(x => x.IsActive, ct);
        if (branch is null)
        {
            branch = new Branch { Id = Guid.NewGuid(), Name = "Local development / التطوير المحلي", IsActive = true };
            db.Branches.Add(branch);
            await db.SaveChangesAsync(ct);
        }
        if (!await roles.RoleExistsAsync("Admin"))
            Require(await roles.CreateAsync(new Role { Id = Guid.NewGuid(), Name = "Admin", IsSystem = true }));
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = name, Email = name + "@example.test",
            FullName = "المشرف المؤقت", IsActive = true, BranchId = branch.Id, CreatedOn = DateTimeOffset.UtcNow
        };
        Require(await users.CreateAsync(user, password));
        Require(await users.AddClaimAsync(user, new Claim(LoginAccessPolicy.TemporaryClaim, "true")));
        Require(await users.AddToRoleAsync(user, "Admin"));
        if (transaction is not null) await transaction.CommitAsync(ct);
    }

    private static void Require(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Temporary administrator setup failed: " +
            string.Join(", ", result.Errors.Select(x => x.Code)));
    }
}
