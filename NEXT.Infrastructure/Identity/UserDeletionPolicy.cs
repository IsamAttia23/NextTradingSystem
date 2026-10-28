using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Exceptions;
using NEXT.Infrastructure.Persistence;
namespace NEXT.Infrastructure.Identity;
public sealed class UserDeletionPolicy(NEXTDbContext db, IConfiguration configuration) : IUserDeletionPolicy
{
    public async Task ValidateAsync(Guid userId, CancellationToken ct)
    {
        var names = configuration.GetSection("Security:AdministratorRoles").Get<string[]>() ?? ["Admin"];
        var adminRoles = db.Roles.Where(x => names.Contains(x.Name!)).Select(x => x.Id);
        var administrators = db.Users.Where(x => x.IsActive && db.UserRoles.Any(r => r.UserId == x.Id && adminRoles.Contains(r.RoleId)));
        if (await administrators.AnyAsync(x => x.Id == userId, ct) && !await administrators.AnyAsync(x => x.Id != userId, ct))
            throw new UnprocessableEntityException("The last active administrator cannot be deleted.");
    }
}
