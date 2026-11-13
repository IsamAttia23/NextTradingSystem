using Microsoft.EntityFrameworkCore;
using NEXT.Application.Features.Dashboard;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Identity;

public sealed class DashboardAccountCounts(NEXTDbContext db) : IDashboardAccountCounts
{
    public Task<(int Users, int ActiveUsers, int Roles)> ReadAsync(CancellationToken ct) =>
        StorageErrors.ExecuteAsync(async () =>
        {
            var users = await db.Users.CountAsync(ct);
            var activeUsers = await db.Users.CountAsync(user => user.IsActive, ct);
            var roles = await db.Roles.CountAsync(ct);
            return (users, activeUsers, roles);
        });
}
