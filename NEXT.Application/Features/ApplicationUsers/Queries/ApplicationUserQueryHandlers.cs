using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Domain.Entities;
using System.Security.Claims;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed class GetApplicationUserByIdHandler(IUserQueryService users) : IRequestHandler<GetApplicationUserByIdQuery, ApplicationUser?>
{
    public Task<ApplicationUser?> Handle(GetApplicationUserByIdQuery request, CancellationToken cancellationToken) =>
        users.FindUserAsync(request.UserId);
}

public sealed class GetApplicationUserByEmailHandler(IUserManagementService users) : IRequestHandler<GetApplicationUserByEmailQuery, ApplicationUser?>
{
    public Task<ApplicationUser?> Handle(GetApplicationUserByEmailQuery request, CancellationToken cancellationToken) =>
        users.FindUserByEmailAsync(request.Email);
}

public sealed class GetApplicationUserByNameHandler(IUserManagementService users) : IRequestHandler<GetApplicationUserByNameQuery, ApplicationUser?>
{
    public Task<ApplicationUser?> Handle(GetApplicationUserByNameQuery request, CancellationToken cancellationToken) =>
        users.FindUserByNameAsync(request.UserName);
}

public sealed class GetApplicationUsersHandler(IUserManagementService users) : IRequestHandler<GetApplicationUsersQuery, IEnumerable<ApplicationUser>>
{
    public Task<IEnumerable<ApplicationUser>> Handle(GetApplicationUsersQuery request, CancellationToken cancellationToken) =>
        users.GetUsersAsync(cancellationToken);
}

public sealed class CheckApplicationUserPasswordHandler(IUserManagementService users) : IRequestHandler<CheckApplicationUserPasswordQuery, bool>
{
    public Task<bool> Handle(CheckApplicationUserPasswordQuery request, CancellationToken cancellationToken) =>
        users.CheckPasswordAsync(request.User, request.Password);
}

public sealed class GetApplicationUserClaimsHandler(IUserManagementService users) : IRequestHandler<GetApplicationUserClaimsQuery, IEnumerable<Claim>>
{
    public Task<IEnumerable<Claim>> Handle(GetApplicationUserClaimsQuery request, CancellationToken cancellationToken) =>
        users.GetUserClaims(request.User);
}

public sealed class GetApplicationUserRolesHandler(IUserManagementService users) : IRequestHandler<GetApplicationUserRolesQuery, IList<string>>
{
    public Task<IList<string>> Handle(GetApplicationUserRolesQuery request, CancellationToken cancellationToken) =>
        users.GetUserRoles(request.User);
}
