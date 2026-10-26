namespace NEXT.Application.DTOs.ApplicationUsers;

/// <summary>Compact user representation intended for user lists.</summary>
public sealed record UserResponseDto(
    Guid Id,
    string FullName,
    string? UserName,
    string? Email,
    bool IsActive,
    Guid BranchId);
