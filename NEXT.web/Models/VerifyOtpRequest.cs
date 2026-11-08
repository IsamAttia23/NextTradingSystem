using System.ComponentModel.DataAnnotations;

namespace NEXT.web.Models;

public sealed record VerifyOtpRequest(Guid ChallengeId,
    [Required(ErrorMessage = "OtpRequired"), RegularExpression("^[0-9]{5}$", ErrorMessage = "OtpFiveDigits")] string Code);
