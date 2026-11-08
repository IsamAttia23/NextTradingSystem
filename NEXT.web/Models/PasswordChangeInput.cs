using System.ComponentModel.DataAnnotations;

namespace NEXT.web.Models;

public sealed class PasswordChangeInput
{
    [Required(ErrorMessage = "NewPasswordRequired"), StringLength(128, MinimumLength = 10, ErrorMessage = "PasswordLengthRange")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "ConfirmPasswordRequired"), Compare(nameof(NewPassword), ErrorMessage = "PasswordConfirmationMismatch")]
    public string ConfirmPassword { get; set; } = "";
}
