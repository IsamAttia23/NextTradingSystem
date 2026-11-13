using MediatR;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Presentation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Dashboard;

public sealed class GetDashboardHandler(IDashboardAccountCounts accounts,
    IReadRepository<RolePermission> grants, IReadRepository<Permission> permissions) : IRequestHandler<GetDashboardQuery, DashboardModel>
{
    public async Task<DashboardModel> Handle(GetDashboardQuery request, CancellationToken ct)
    {
        // One scoped DbContext: keep database operations sequential and project counts only.
        var (totalUsers, activeUsers, roleCount) = await accounts.ReadAsync(ct);
        var permissionCount = await permissions.CountAsync(cancellationToken: ct);
        var grantCount = await grants.CountAsync(cancellationToken: ct);
        return new(totalUsers, activeUsers, roleCount, permissionCount, grantCount);
    }
}
