using System.ComponentModel.DataAnnotations;

namespace NEXT.web.Models;

public sealed class RecoveryEmailInput
{
    [Required(ErrorMessage = "EmailRequired"), EmailAddress(ErrorMessage = "InvalidEmailAddress"), StringLength(256, ErrorMessage = "FieldTooLong")]
    public string Email { get; set; } = "";
}
