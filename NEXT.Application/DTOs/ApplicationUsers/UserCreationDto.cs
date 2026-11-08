using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.DTOs.ApplicationUsers;

public sealed record UserCreationDto
{
    [Required, StringLength(200)]
    public string FullName { get; init; } = string.Empty;

    [Required, StringLength(256)]
    public string UserName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Phone, StringLength(32)]
    public string? PhoneNumber { get; init; }

    /// <summary>Optional profile image content. The current model stores image bytes.</summary>
    public byte[]? AvatarUrl { get; init; }

    // Retained for form compatibility; account creation ignores client-provided passwords.
    public string Password { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;

    [Required]
    public Guid BranchId { get; init; }

    [StringLength(256)]
    public string? Role { get; init; }
}
