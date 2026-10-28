using MediatR;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Features.ApplicationUsers.Queries;
using NEXT.Application.Features.Security.Roles;
using NEXT.Application.Features.Security.RolePermissions;
using NEXT.web.Models;

namespace NEXT.web.Controllers;

public sealed class HomeController(ISender sender,
    IEntityServices<Guid, PermissionDto, PermissionCreationDto, PermissionUpdateDto> permissions) : WorkspaceController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Queries share a scoped DbContext; await them sequentially.
        var users = (await sender.Send(new GetApplicationUsersQuery(), ct)).ToArray();
        var roles = await sender.Send(new GetRolesQuery(), ct);
        var grants = await sender.Send(new GetRolePermissionsQuery(), ct);
        var page = await permissions.GetAll(new PaginationQuery { PageSize = 1 });
        return View(new DashboardModel(users.Length, users.Count(x => x.IsActive), roles.Count, page.TotalCount, grants.Count));
    }
}
