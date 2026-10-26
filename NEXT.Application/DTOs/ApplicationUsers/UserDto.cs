namespace NEXT.Application.DTOs.ApplicationUsers;

/// <summary>
/// Detailed administrative user representation. Password hashes, security stamps, and tokens are never exposed.
/// </summary>
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
    int AccessFailedCount,
    bool LockoutEnabled,
    DateTimeOffset? LockoutEnd,
    DateTime? LastLogin,
    DateTime? LastInvalidLoginAttempt,
    DateTimeOffset CreatedOn,
    string? CreatedBy,
    DateTimeOffset? UpdatedOn,
    DateTimeOffset? DeletedAt,
    Guid? DeletedBy,
    string? DeletionReason,
    byte[]? AvatarUrl,
    Guid BranchId,
    IReadOnlyList<string> Roles);
