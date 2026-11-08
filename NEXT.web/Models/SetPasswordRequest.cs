using System.ComponentModel.DataAnnotations;

namespace NEXT.web.Models;

public sealed record SetPasswordRequest(
    [Required(ErrorMessage = "InvalidSecurityToken"), StringLength(64, MinimumLength = 64, ErrorMessage = "InvalidSecurityToken")] string Token,
    [Required(ErrorMessage = "NewPasswordRequired"), StringLength(128, MinimumLength = 10, ErrorMessage = "PasswordLengthRange")] string NewPassword);
