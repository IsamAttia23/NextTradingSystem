using Mapster;
using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.ApplicationUsers.Commands;

public sealed class CreateApplicationUserHandler(
    IUserCommandService commands,
    IUserQueryService queries) : IRequestHandler<CreateApplicationUserCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(CreateApplicationUserCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.User);
        var dto = request.User;
        var errors = new Dictionary<string, string[]>();

        if (dto.BranchId == Guid.Empty)
            errors[nameof(dto.BranchId)] = ["A valid branch is required."];
        if (await queries.IsUserNameTakenAsync(dto.UserName))
            errors[nameof(dto.UserName)] = ["This username is already in use."];
        if (await queries.IsEmailTakenAsync(dto.Email))
            errors[nameof(dto.Email)] = ["This email address is already in use."];

        if (errors.Count != 0)
            throw new AppValidationException(errors);

        var user = dto.Adapt<ApplicationUser>();
        var result = await commands.CreateUserAsync(user, dto.Password);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(dto.Role))
            return result;

        // Identity may fail role assignment after the user has been created; report the failure explicitly.
        return await commands.AddUserToRole(user, dto.Role.Trim());
    }
}

public sealed class UpdateApplicationUserHandler(
    IUserQueryService queries,
    IUserCommandService commands) : IRequestHandler<UpdateApplicationUserCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(UpdateApplicationUserCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.User);
        if (!Guid.TryParse(request.UserId, out var userId))
            throw new AppValidationException(new Dictionary<string, string[]> {
                [nameof(request.UserId)] = ["The user ID is invalid."]
            });

        var user = await queries.FindUserAsync(request.UserId);
        if (user is null)
            throw new NotFoundException($"User '{request.UserId}' was not found.");

        var dto = request.User;
        var errors = new Dictionary<string, string[]>();
        if (dto.BranchId == Guid.Empty)
            errors[nameof(dto.BranchId)] = ["A valid branch is required."];
        if (await queries.IsUserNameTakenAsync(dto.UserName, userId.ToString()))
            errors[nameof(dto.UserName)] = ["This username is already in use by another user."];
        if (await queries.IsEmailTakenAsync(dto.Email, userId.ToString()))
            errors[nameof(dto.Email)] = ["This email address is already in use by another user."];

        if (errors.Count != 0)
            throw new AppValidationException(errors);

        dto.Adapt(user);
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
