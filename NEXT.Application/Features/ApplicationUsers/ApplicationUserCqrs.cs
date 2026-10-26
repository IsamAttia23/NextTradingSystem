using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Identity;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence.UsersManagment;

namespace NEXT.Application.Features.ApplicationUsers;

// Commands
public sealed record CreateApplicationUserCommand(ApplicationUser User, string Password) : IRequest<IdentityResult>;
public sealed record UpdateApplicationUserCommand(ApplicationUser User) : IRequest<IdentityResult>;
public sealed record ChangeApplicationUserPasswordCommand(ApplicationUser User, string CurrentPassword, string NewPassword) : IRequest<IdentityResult>;
public sealed record SetApplicationUserNameCommand(ApplicationUser User, string UserName) : IRequest<IdentityResult>;
public sealed record SetApplicationUserEmailCommand(ApplicationUser User, string Email) : IRequest<IdentityResult>;
public sealed record SetApplicationUserPhoneNumberCommand(ApplicationUser User, string PhoneNumber) : IRequest<IdentityResult>;
public sealed record AddApplicationUserToRoleCommand(ApplicationUser User, string Role) : IRequest<IdentityResult>;

// Queries
public sealed record GetApplicationUserByIdQuery(string UserId) : IRequest<ApplicationUser?>;
public sealed record GetApplicationUserByEmailQuery(string Email) : IRequest<ApplicationUser?>;
public sealed record GetApplicationUserByNameQuery(string UserName) : IRequest<ApplicationUser?>;
public sealed record GetApplicationUsersQuery : IRequest<IEnumerable<ApplicationUser>>;
public sealed record CheckApplicationUserPasswordQuery(ApplicationUser User, string Password) : IRequest<bool>;
public sealed record GetApplicationUserClaimsQuery(ApplicationUser User) : IRequest<IEnumerable<Claim>>;
public sealed record GetApplicationUserRolesQuery(ApplicationUser User) : IRequest<IList<string>>;

public sealed class CreateApplicationUserHandler(IUserManagmentServices users) : IRequestHandler<CreateApplicationUserCommand, IdentityResult>
{
    public Task<IdentityResult> Handle(CreateApplicationUserCommand request, CancellationToken cancellationToken) =>
        users.CreateUserAsync(request.User, request.Password);
}

public sealed class UpdateApplicationUserHandler(IUserManagmentServices users) : IRequestHandler<UpdateApplicationUserCommand, IdentityResult>
{
    public Task<IdentityResult> Handle(UpdateApplicationUserCommand request, CancellationToken cancellationToken) =>
        users.UpdateUserAsync(request.User);
}

public sealed class ChangeApplicationUserPasswordHandler(IUserManagmentServices users) : IRequestHandler<ChangeApplicationUserPasswordCommand, IdentityResult>
{
    public Task<IdentityResult> Handle(ChangeApplicationUserPasswordCommand request, CancellationToken cancellationToken) =>
        users.ChangePasswordAsync(request.User, request.CurrentPassword, request.NewPassword);
}

public sealed class SetApplicationUserNameHandler(IUserManagmentServices users) : IRequestHandler<SetApplicationUserNameCommand, IdentityResult>
{
    public Task<IdentityResult> Handle(SetApplicationUserNameCommand request, CancellationToken cancellationToken) =>
        users.SetUserNameAsync(request.User, request.UserName);
}

public sealed class SetApplicationUserEmailHandler(IUserManagmentServices users) : IRequestHandler<SetApplicationUserEmailCommand, IdentityResult>
{
    public Task<IdentityResult> Handle(SetApplicationUserEmailCommand request, CancellationToken cancellationToken) =>
        users.SetUserEmailAsync(request.User, request.Email);
}

public sealed class SetApplicationUserPhoneNumberHandler(IUserManagmentServices users) : IRequestHandler<SetApplicationUserPhoneNumberCommand, IdentityResult>
{
    public Task<IdentityResult> Handle(SetApplicationUserPhoneNumberCommand request, CancellationToken cancellationToken) =>
        users.SetPhoneNumberAsync(request.User, request.PhoneNumber);
}

public sealed class AddApplicationUserToRoleHandler(IUserManagmentServices users) : IRequestHandler<AddApplicationUserToRoleCommand, IdentityResult>
{
    public Task<IdentityResult> Handle(AddApplicationUserToRoleCommand request, CancellationToken cancellationToken) =>
        users.AddUserToRole(request.User, request.Role);
}

public sealed class GetApplicationUserByIdHandler(IUserManagmentServices users) : IRequestHandler<GetApplicationUserByIdQuery, ApplicationUser?>
{
    public Task<ApplicationUser?> Handle(GetApplicationUserByIdQuery request, CancellationToken cancellationToken) =>
        users.FindUserAsync(request.UserId);
}

public sealed class GetApplicationUserByEmailHandler(IUserManagmentServices users) : IRequestHandler<GetApplicationUserByEmailQuery, ApplicationUser?>
{
    public Task<ApplicationUser?> Handle(GetApplicationUserByEmailQuery request, CancellationToken cancellationToken) =>
        users.FindUserByEmailAsync(request.Email);
}

public sealed class GetApplicationUserByNameHandler(IUserManagmentServices users) : IRequestHandler<GetApplicationUserByNameQuery, ApplicationUser?>
{
    public Task<ApplicationUser?> Handle(GetApplicationUserByNameQuery request, CancellationToken cancellationToken) =>
        users.FindUserByNameAsync(request.UserName);
}

public sealed class GetApplicationUsersHandler(IUserManagmentServices users) : IRequestHandler<GetApplicationUsersQuery, IEnumerable<ApplicationUser>>
{
    public Task<IEnumerable<ApplicationUser>> Handle(GetApplicationUsersQuery request, CancellationToken cancellationToken) =>
        users.GetUsersAsync();
}

public sealed class CheckApplicationUserPasswordHandler(IUserManagmentServices users) : IRequestHandler<CheckApplicationUserPasswordQuery, bool>
{
    public Task<bool> Handle(CheckApplicationUserPasswordQuery request, CancellationToken cancellationToken) =>
        users.CheckPasswordAsync(request.User, request.Password);
}

public sealed class GetApplicationUserClaimsHandler(IUserManagmentServices users) : IRequestHandler<GetApplicationUserClaimsQuery, IEnumerable<Claim>>
{
    public Task<IEnumerable<Claim>> Handle(GetApplicationUserClaimsQuery request, CancellationToken cancellationToken) =>
        users.GetUserClaims(request.User);
}

public sealed class GetApplicationUserRolesHandler(IUserManagmentServices users) : IRequestHandler<GetApplicationUserRolesQuery, IList<string>>
{
    public Task<IList<string>> Handle(GetApplicationUserRolesQuery request, CancellationToken cancellationToken) =>
        users.GetUserRoles(request.User);
}
