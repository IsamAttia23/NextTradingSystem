using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Presentation;
using NEXT.Domain.Entities;
namespace NEXT.Application.Features.Dashboard;
public sealed record GetDashboardQuery : IRequest<DashboardModel>;
public sealed class GetDashboardHandler(IUserQueryService users, IRoleManagementService roles,
    IRolePermissionService grants, IRepository<Permission> permissions) : IRequestHandler<GetDashboardQuery, DashboardModel>
{
    public async Task<DashboardModel> Handle(GetDashboardQuery request, CancellationToken ct)
    {
        var list = (await users.GetUsersAsync(ct)).ToArray();
        // These ports share a scoped DbContext, so requests are intentionally sequential.
        var allRoles = await roles.GetAllAsync(ct);
        var allGrants = await grants.GetAllAsync(ct);
        return new(list.Length, list.Count(x => x.IsActive), allRoles.Count, await permissions.CountAsync(cancellationToken: ct), allGrants.Count);
    }
}
