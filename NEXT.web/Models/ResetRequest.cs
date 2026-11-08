using System.ComponentModel.DataAnnotations;

namespace NEXT.web.Models;

public sealed record ResetRequest(
    [Required(ErrorMessage = "EmailRequired"), EmailAddress(ErrorMessage = "InvalidEmailAddress"), StringLength(256, ErrorMessage = "FieldTooLong")] string Email);
