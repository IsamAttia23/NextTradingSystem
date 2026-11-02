using MediatR;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Presentation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Dashboard;

public sealed class GetDashboardHandler(IRepository<ApplicationUser> users, IRepository<Role> roles,
    IRepository<RolePermission> grants, IRepository<Permission> permissions) : IRequestHandler<GetDashboardQuery, DashboardModel>
{
    public async Task<DashboardModel> Handle(GetDashboardQuery request, CancellationToken ct)
    {
        // One scoped DbContext: keep database operations sequential and project counts only.
        var totalUsers = await users.CountAsync(cancellationToken: ct);
        var activeUsers = await users.CountAsync(x => x.IsActive, ct);
        var roleCount = await roles.CountAsync(cancellationToken: ct);
        var permissionCount = await permissions.CountAsync(cancellationToken: ct);
        var grantCount = await grants.CountAsync(cancellationToken: ct);
        return new(totalUsers, activeUsers, roleCount, permissionCount, grantCount);
    }
}
