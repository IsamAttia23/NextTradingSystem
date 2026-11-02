using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NEXT.Application.Abstractions.Identity;
using NEXT.Infrastructure.Persistence;
namespace NEXT.Infrastructure.Identity;
public sealed class AdministratorLookup(NEXTDbContext db, IConfiguration configuration) : IAdministratorLookup
{
    public async Task<bool> IsLastActiveAsync(Guid userId, CancellationToken ct)
    {
        var names = (configuration.GetSection("Security:AdministratorRoles").Get<string[]>() ?? ["Admin"])
            .Select(x => x.Trim().ToUpperInvariant()).ToArray();
        var roleIds = db.Roles.Where(x => names.Contains(x.NormalizedName!)).Select(x => x.Id);
        var administrators = db.Users.Where(x => x.IsActive && db.UserRoles.Any(r => r.UserId == x.Id && roleIds.Contains(r.RoleId)));
        return await StorageErrors.ExecuteAsync(async () =>
            await administrators.AnyAsync(x => x.Id == userId, ct) && !await administrators.AnyAsync(x => x.Id != userId, ct));
    }
}
