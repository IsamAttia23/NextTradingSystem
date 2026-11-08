using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.DTOs.Authentication;

public sealed class LoginDto
{
    [Required(ErrorMessage = "UserNameRequired"), StringLength(256, ErrorMessage = "FieldTooLong")]
    public string UserName { get; set; } = "";
    [Required(ErrorMessage = "PasswordRequired")]
    public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
