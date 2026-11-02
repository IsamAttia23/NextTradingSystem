using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.DTOs.ApplicationUsers;

public sealed record UserUpdateDto
{
    [Required, StringLength(200)]
    public string FullName { get; init; } = string.Empty;

    [Required, StringLength(256)]
    public string UserName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Phone, StringLength(32)]
    public string? PhoneNumber { get; init; }

    /// <summary>Optional replacement profile image content. Null preserves the current image.</summary>
    public byte[]? AvatarUrl { get; init; }

    public bool IsActive { get; init; }

    [Required]
    public Guid BranchId { get; init; }
}
