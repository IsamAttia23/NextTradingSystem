using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.DTOs.Authentication;

public sealed class VerifyLoginDto
{
    [Required, StringLength(32)] public string Code { get; set; } = "";
    public bool UseRecoveryCode { get; set; }
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
