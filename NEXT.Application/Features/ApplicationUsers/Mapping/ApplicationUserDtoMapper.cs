using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.ApplicationUsers.Mapping;

/// <summary>Single responsibility: maps ApplicationUser entities to/from feature DTOs.</summary>
public static class ApplicationUserDtoMapper
{
    public static UserResponseDto ToResponseDto(ApplicationUser user) => new(
        user.Id, user.FullName, user.UserName, user.Email, user.IsActive, user.BranchId);

    public static UserDto ToDto(ApplicationUser user, IReadOnlyList<string> roles) => new(
        user.Id,
        user.FullName,
        user.UserName,
        user.Email,
        user.PhoneNumber,
        user.EmailConfirmed,
        user.PhoneNumberConfirmed,
        user.TwoFactorEnabled,
        user.IsActive,
        user.AccessFailedCount,
        user.LockoutEnabled,
        user.LockoutEnd,
        user.LastLogin,
        user.LastInvalidLoginAttempt,
        user.CreatedOn,
        user.CreatedBy,
        user.UpdatedOn,
        user.DeletedAt,
        user.DeletedBy,
        user.DeletionReason,
        user.AvatarUrl,
        user.BranchId,
        roles);

    public static ApplicationUser ToEntity(UserCreationDto dto) => new()
    {
        FullName = dto.FullName.Trim(),
        UserName = dto.UserName.Trim(),
        Email = dto.Email.Trim(),
        PhoneNumber = dto.PhoneNumber?.Trim(),
        IsActive = dto.IsActive,
        BranchId = dto.BranchId,
        CreatedOn = DateTimeOffset.UtcNow
    };

    public static void ApplyUpdate(ApplicationUser user, UserUpdateDto dto)
    {
        user.FullName = dto.FullName.Trim();
        user.UserName = dto.UserName.Trim();
        user.Email = dto.Email.Trim();
        user.PhoneNumber = dto.PhoneNumber?.Trim();
        user.IsActive = dto.IsActive;
        user.BranchId = dto.BranchId;
        user.UpdatedOn = DateTimeOffset.UtcNow;
    }
}
