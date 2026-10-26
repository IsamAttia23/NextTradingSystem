namespace NEXT.Application.DTOs.ApplicationUsers;

/// <summary>Detailed user representation. Sensitive authentication internals are intentionally excluded.</summary>
public sealed record UserDto(
    Guid Id,
    string FullName,
    string? UserName,
    string? Email,
    string? PhoneNumber,
    bool EmailConfirmed,
    bool PhoneNumberConfirmed,
    bool TwoFactorEnabled,
    bool IsActive,
    DateTime? LastLogin,
    DateTime? LastInvalidLoginAttempt,
    DateTimeOffset CreatedOn,
    string? CreatedBy,
    DateTimeOffset? UpdatedOn,
    DateTimeOffset? DeletedAt,
    Guid? DeletedBy,
    string? DeletionReason,
    byte[]? AvatarUrl,
    Guid BranchId);
