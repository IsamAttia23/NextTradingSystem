using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.DTOs.Authentication;

public sealed class LoginDto
{
    [Required, StringLength(256)] public string UserName { get; set; } = "";
    [Required] public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
