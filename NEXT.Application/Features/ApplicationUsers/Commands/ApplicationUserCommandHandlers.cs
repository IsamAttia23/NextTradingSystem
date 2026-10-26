using MediatR;
using NEXT.Application.Abstractions.Identity;

namespace NEXT.Application.Features.ApplicationUsers.Commands;

public sealed class CreateApplicationUserHandler(IUserCommandService users) : IRequestHandler<CreateApplicationUserCommand, UserOperationResult>
{
    public Task<UserOperationResult> Handle(CreateApplicationUserCommand request, CancellationToken cancellationToken) =>
        users.CreateUserAsync(request.User, request.Password);
}

public sealed class UpdateApplicationUserHandler(IUserCommandService users) : IRequestHandler<UpdateApplicationUserCommand, UserOperationResult>
{
    public Task<UserOperationResult> Handle(UpdateApplicationUserCommand request, CancellationToken cancellationToken) =>
        users.UpdateUserAsync(request.User);
}

public sealed class ChangeApplicationUserPasswordHandler(IUserCommandService users) : IRequestHandler<ChangeApplicationUserPasswordCommand, UserOperationResult>
{
    public Task<UserOperationResult> Handle(ChangeApplicationUserPasswordCommand request, CancellationToken cancellationToken) =>
        users.ChangePasswordAsync(request.User, request.CurrentPassword, request.NewPassword);
}

public sealed class SetApplicationUserNameHandler(IUserCommandService users) : IRequestHandler<SetApplicationUserNameCommand, UserOperationResult>
{
    public Task<UserOperationResult> Handle(SetApplicationUserNameCommand request, CancellationToken cancellationToken) =>
        users.SetUserNameAsync(request.User, request.UserName);
}

public sealed class SetApplicationUserEmailHandler(IUserCommandService users) : IRequestHandler<SetApplicationUserEmailCommand, UserOperationResult>
{
    public Task<UserOperationResult> Handle(SetApplicationUserEmailCommand request, CancellationToken cancellationToken) =>
        users.SetUserEmailAsync(request.User, request.Email);
}

public sealed class SetApplicationUserPhoneNumberHandler(IUserCommandService users) : IRequestHandler<SetApplicationUserPhoneNumberCommand, UserOperationResult>
{
    public Task<UserOperationResult> Handle(SetApplicationUserPhoneNumberCommand request, CancellationToken cancellationToken) =>
        users.SetPhoneNumberAsync(request.User, request.PhoneNumber);
}

public sealed class AddApplicationUserToRoleHandler(IUserCommandService users) : IRequestHandler<AddApplicationUserToRoleCommand, UserOperationResult>
{
    public Task<UserOperationResult> Handle(AddApplicationUserToRoleCommand request, CancellationToken cancellationToken) =>
        users.AddUserToRole(request.User, request.Role);
}
