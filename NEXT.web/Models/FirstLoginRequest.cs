using System.ComponentModel.DataAnnotations;

namespace NEXT.web.Models;

public sealed record FirstLoginRequest(
    [Required(ErrorMessage = "UserNameRequired"), StringLength(256, ErrorMessage = "FieldTooLong")] string UserName,
    [Required(ErrorMessage = "PasswordRequired"), StringLength(128, ErrorMessage = "FieldTooLong")] string Password);
