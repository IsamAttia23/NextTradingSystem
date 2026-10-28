using System.ComponentModel.DataAnnotations;
namespace NEXT.Application.DTOs;
public sealed class DeletionRequestDto
{
    [Required(ErrorMessage = "DeletionReasonRequired"), StringLength(500, ErrorMessage = "DeletionReasonTooLong")]
    public string Reason { get; set; } = "";
}
