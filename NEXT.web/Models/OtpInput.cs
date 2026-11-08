using System.ComponentModel.DataAnnotations;

namespace NEXT.web.Models;

public sealed class OtpInput
{
    [Required(ErrorMessage = "OtpRequired"), RegularExpression("^[0-9]{5}$", ErrorMessage = "OtpFiveDigits")]
    public string Code { get; set; } = "";
}
