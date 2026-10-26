using MediatR;
using Microsoft.AspNetCore.Identity;
using NEXT.Application.Abstractions.Identity;

namespace NEXT.Application.Features.ApplicationUsers.Commands;

public sealed class CreateApplicationUserHandler(IUserManagementService users) : IRequestHandler<CreateApplicationUserCommand, IdentityResult>
{
    public Task<IdentityResult> Handle(CreateApplicationUserCommand request, CancellationToken cancellationToken) =>
        users.CreateUserAsync(request.User, request.Password);
}

public sealed class UpdateApplicationUserHandler(IUserManagementService users) : IRequestHandler<UpdateApplicationUserCommand, IdentityResult>
{
    public Task<IdentityResult> Handle(UpdateApplicationUserCommand request, CancellationToken cancellationToken) =>
        users.UpdateUserAsync(request.User);
}

public sealed class ChangeApplicationUserPasswordHandler(IUserManagementService users) : IRequestHandler<ChangeApplicationUserPasswordCommand, IdentityResult>
{
    public Task<IdentityResult> Handle(ChangeApplicationUserPasswordCommand request, CancellationToken cancellationToken) =>
        users.ChangePasswordAsync(request.User, request.CurrentPassword, request.NewPassword);
}

public sealed class SetApplicationUserNameHandler(IUserManagementService users) : IRequestHandler<SetApplicationUserNameCommand, IdentityResult>
{
    public Task<IdentityResult> Handle(SetApplicationUserNameCommand request, CancellationToken cancellationToken) =>
        users.SetUserNameAsync(request.User, request.UserName);
}

public sealed class SetApplicationUserEmailHandler(IUserManagementService users) : IRequestHandler<SetApplicationUserEmailCommand, IdentityResult>
{
    public Task<IdentityResult> Handle(SetApplicationUserEmailCommand request, CancellationToken cancellationToken) =>
        users.SetUserEmailAsync(request.User, request.Email);
}

public sealed class SetApplicationUserPhoneNumberHandler(IUserManagementService users) : IRequestHandler<SetApplicationUserPhoneNumberCommand, IdentityResult>
{
    public Task<IdentityResult> Handle(SetApplicationUserPhoneNumberCommand request, CancellationToken cancellationToken) =>
        users.SetPhoneNumberAsync(request.User, request.PhoneNumber);
}

public sealed class AddApplicationUserToRoleHandler(IUserManagementService users) : IRequestHandler<AddApplicationUserToRoleCommand, IdentityResult>
{
    public Task<IdentityResult> Handle(AddApplicationUserToRoleCommand request, CancellationToken cancellationToken) =>
        users.AddUserToRole(request.User, request.Role);
}
