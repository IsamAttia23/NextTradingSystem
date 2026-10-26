using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Features.ApplicationUsers.Mapping;

namespace NEXT.Application.Features.ApplicationUsers.Commands;

public sealed class CreateApplicationUserHandler(IUserCommandService commands) : IRequestHandler<CreateApplicationUserCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(CreateApplicationUserCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.User);
        if (request.User.BranchId == Guid.Empty)
            return UserOperationResult.Failure([new UserOperationError("BranchRequired", "A valid branch is required.")]);

        var user = ApplicationUserDtoMapper.ToEntity(request.User);
        var result = await commands.CreateUserAsync(user, request.User.Password);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(request.User.Role))
            return result;

        return await commands.AddUserToRole(user, request.User.Role.Trim());
    }
}

public sealed class UpdateApplicationUserHandler(
    IUserQueryService queries,
    IUserCommandService commands) : IRequestHandler<UpdateApplicationUserCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(UpdateApplicationUserCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.User);
        if (!Guid.TryParse(request.UserId, out _))
            return UserOperationResult.Failure([new UserOperationError("InvalidUserId", "The user ID is invalid.")]);
        if (request.User.BranchId == Guid.Empty)
            return UserOperationResult.Failure([new UserOperationError("BranchRequired", "A valid branch is required.")]);

        var user = await queries.FindUserAsync(request.UserId);
        if (user is null)
            return UserOperationResult.Failure([new UserOperationError("UserNotFound", "The user was not found.")]);

        ApplicationUserDtoMapper.ApplyUpdate(user, request.User);
        return await commands.UpdateUserAsync(user);
    }
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
