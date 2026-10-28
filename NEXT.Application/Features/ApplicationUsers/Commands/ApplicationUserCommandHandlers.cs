using NEXT.Application.Abstractions.Media;
using Mapster;
using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence.Repositories;

namespace NEXT.Application.Features.ApplicationUsers.Commands;

public sealed class CreateApplicationUserHandler(
    IUserCommandService commands,
    IUserQueryService queries,
    IBranchRepository branches,
    IProfileImageProcessor images,
    IEntityValidator<UserCreationDto> validator) : IRequestHandler<CreateApplicationUserCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(CreateApplicationUserCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.User);
        var dto = request.User;
        validator.Validate(dto);

        var errors = new Dictionary<string, string[]>();
        if (dto.BranchId == Guid.Empty)
            errors[nameof(dto.BranchId)] = ["A valid branch is required."];
        else if (!await branches.AnyAsync(x => x.Id == dto.BranchId && x.IsActive, cancellationToken))
            errors[nameof(dto.BranchId)] = ["Select an active branch."];
        if (await queries.IsUserNameTakenAsync(dto.UserName))
            errors[nameof(dto.UserName)] = ["This username is already in use."];
        if (await queries.IsEmailTakenAsync(dto.Email))
            errors[nameof(dto.Email)] = ["This email address is already in use."];
        if (errors.Count != 0)
            throw new AppValidationException(errors);

        var avatar = dto.AvatarUrl is null ? null : images.Normalize(dto.AvatarUrl);
        var user = dto.Adapt<ApplicationUser>();
        user.AvatarUrl = avatar;
        var result = await commands.CreateUserAsync(user, dto.Password);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(dto.Role))
            return result;

        var roleResult = await commands.AddUserToRole(user, dto.Role.Trim());
        if (roleResult.Succeeded)
            return roleResult;

        var cleanupResult = await commands.DeleteUserAsync(user);
        if (cleanupResult.Succeeded)
            return roleResult;

        return UserOperationResult.Failure(roleResult.Errors.Concat(cleanupResult.Errors).Append(
            new UserOperationError("UserCleanupFailed", "The user could not be removed after role assignment failed.")));
    }
}

public sealed class UpdateApplicationUserHandler(
    IUserQueryService queries,
    IUserCommandService commands,
    IBranchRepository branches,
    IProfileImageProcessor images,
    IEntityValidator<UserUpdateDto> validator) : IRequestHandler<UpdateApplicationUserCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(UpdateApplicationUserCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.User);
        validator.Validate(request.User);

        if (!Guid.TryParse(request.UserId, out var userId))
            throw new AppValidationException(new Dictionary<string, string[]> {
                [nameof(request.UserId)] = ["The user ID is invalid."]
            });

        var user = await queries.FindUserAsync(request.UserId);
        if (user is null)
            throw new NotFoundException($"User '{request.UserId}' was not found.");

        await ValidateUniqueIdentityFieldsAsync(queries, request.User.UserName, request.User.Email, userId);
        if (!await branches.AnyAsync(x => x.Id == request.User.BranchId && (x.IsActive || x.Id == user.BranchId), cancellationToken))
            throw new AppValidationException(new Dictionary<string, string[]> { ["BranchId"] = ["Select a valid branch."] });
        if (request.User.BranchId == Guid.Empty)
            throw new AppValidationException(new Dictionary<string, string[]> {
                [nameof(request.User.BranchId)] = ["A valid branch is required."]
            });

        var avatar = request.User.AvatarUrl is null ? user.AvatarUrl : images.Normalize(request.User.AvatarUrl);
        request.User.Adapt(user);
        user.AvatarUrl = avatar;
        return await commands.UpdateUserAsync(user);
    }

    internal static async Task ValidateUniqueIdentityFieldsAsync(IUserQueryService queries, string userName, string email, Guid? excludingId = null)
    {
        var errors = new Dictionary<string, string[]>();
        if (await queries.IsUserNameTakenAsync(userName, excludingId?.ToString()))
            errors[nameof(UserUpdateDto.UserName)] = ["This username is already in use by another user."];
        if (await queries.IsEmailTakenAsync(email, excludingId?.ToString()))
            errors[nameof(UserUpdateDto.Email)] = ["This email address is already in use by another user."];
        if (errors.Count > 0)
            throw new AppValidationException(errors);
    }
}

public sealed class ChangeApplicationUserPasswordHandler(IUserCommandService users) : IRequestHandler<ChangeApplicationUserPasswordCommand, UserOperationResult>
{
    public Task<UserOperationResult> Handle(ChangeApplicationUserPasswordCommand request, CancellationToken cancellationToken) =>
        users.ChangePasswordAsync(request.User, request.CurrentPassword, request.NewPassword);
}

public sealed class SetApplicationUserNameHandler(IUserCommandService users, IUserQueryService queries) : IRequestHandler<SetApplicationUserNameCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(SetApplicationUserNameCommand request, CancellationToken cancellationToken)
    {
        if (await queries.IsUserNameTakenAsync(request.UserName, request.User.Id.ToString()))
            throw new AppValidationException(new Dictionary<string, string[]> { ["UserName"] = ["This username is already in use."] });
        return await users.SetUserNameAsync(request.User, request.UserName.Trim());
    }
}

public sealed class SetApplicationUserEmailHandler(IUserCommandService users, IUserQueryService queries) : IRequestHandler<SetApplicationUserEmailCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(SetApplicationUserEmailCommand request, CancellationToken cancellationToken)
    {
        if (await queries.IsEmailTakenAsync(request.Email, request.User.Id.ToString()))
            throw new AppValidationException(new Dictionary<string, string[]> { ["Email"] = ["This email address is already in use."] });
        return await users.SetUserEmailAsync(request.User, request.Email.Trim());
    }
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

public sealed class AssignUserRoleHandler(IUserQueryService queries, IUserCommandService commands)
    : IRequestHandler<AssignUserRoleCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(AssignUserRoleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Role))
            throw new AppValidationException(new Dictionary<string, string[]> { ["Role"] = ["Select a role."] });
        var user = await queries.FindUserAsync(request.UserId.ToString())
            ?? throw new NotFoundException("User was not found.");
        return await commands.AddUserToRole(user, request.Role.Trim());
    }
}
