using System.ComponentModel.DataAnnotations;

namespace NEXT.web.Models.Auth;

public sealed class VerifyLoginInput
{
    [Required, StringLength(32)] public string Code { get; set; } = "";
    public bool UseRecoveryCode { get; set; }
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
