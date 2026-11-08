using System.ComponentModel.DataAnnotations;
using MapsterMapper;
using MediatR;
using NEXT.Application.Abstractions.Media;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.ApplicationUsers.Commands;

public sealed class UpdateApplicationUserHandler(IUserCommandService commands, ICurrentUserService currentUser,
    UserDtoReader reader, UserIdentityRules rules, IProfileImageProcessor images,
    IEntityValidator<UserUpdateDto> validator, IMapper mapper, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateApplicationUserCommand, UserOperationResult>
{
    public async Task<UserOperationResult> Handle(UpdateApplicationUserCommand request, CancellationToken ct)
    {
        var userId = request.ProfileOnly
            ? currentUser.UserId ?? throw new NotFoundException("Current user was not found.")
            : RequestValidation.Identifier(request.UserId);
        var user = await reader.RequireAsync(userId, ct);
        // Profile requests cannot choose another account, branch, status, or role.
        var input = request.ProfileOnly
            ? request.User with
            {
                BranchId = user.BranchId,
                IsActive = user.IsActive
            }
            : request.User with { IsActive = user.IsActive };
        validator.Validate(input);
        var changePassword = input.NewPassword is not null;
        var hasCurrentPassword = !string.IsNullOrEmpty(input.CurrentPassword);
        var invalidPasswordChange = changePassword && (!request.ProfileOnly || !hasCurrentPassword);
        var currentPasswordWithoutNewPassword = !changePassword && hasCurrentPassword;
        if (invalidPasswordChange || currentPasswordWithoutNewPassword)
        {
            RequestValidation.Fail("CurrentPassword", "ProfilePasswordPairRequired");
        }
        if (!request.ProfileOnly)
        {
            await rules.BranchAsync(input.BranchId, user.BranchId, ct);
        }
        var email = input.Email.Trim();
        var phone = input.PhoneNumber?.Trim() ?? "";
        await rules.UniqueAsync(input.UserName.Trim(), email, user.Id, ct);
        var emailChanged = !string.Equals(user.Email, email, StringComparison.Ordinal);
        var phoneChanged = !string.Equals(user.PhoneNumber ?? "", phone, StringComparison.Ordinal);
        var avatar = input.AvatarUrl is null ? null : await images.NormalizeAsync(input.AvatarUrl, ct);
        return await UserOperationTransaction.ExecuteAsync(unitOfWork, async token =>
        {
            mapper.Map(input with
            {
                AvatarUrl = avatar
            }, user);
            var result = await commands.UpdateUserAsync(user, token);
            if (result.Succeeded && emailChanged)
            {
                result = await commands.SetUserEmailAsync(user, email, token);
            }
            if (result.Succeeded && phoneChanged)
            {
                result = await commands.SetPhoneNumberAsync(user, phone, token);
            }
            if (result.Succeeded && changePassword)
            {
                result = await commands.ChangePasswordAsync(user, input.CurrentPassword!, input.NewPassword!, token);
            }
            return result with
            {
                RequiresSignIn = result.Succeeded && request.ProfileOnly &&
                                (emailChanged || phoneChanged || changePassword)
            };
        }, ct);
    }
}
