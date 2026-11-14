namespace NEXT.Application.DTOs.ApplicationUsers;

public sealed record UserResponseDto(
    Guid Id,
    string FullName,
    string? UserName,
    string? Email,
    bool IsActive,
    Guid BranchId,
    bool HasRole = false,
    bool RequiresAdministratorUnlock = false,
    DateTimeOffset? LockoutEnd = null);
