using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Branches;
public class BranchInputDto
{
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [StringLength(500)] public string? Address { get; set; }
    [Phone, StringLength(32)] public string? Phone { get; set; }
     public bool IsActive { get; set; } = true;
}
public sealed class BranchDto : BranchInputDto
{
    public Guid Id { get; set; }

}
