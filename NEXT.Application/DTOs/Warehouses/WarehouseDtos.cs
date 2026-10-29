using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Warehouses;
public class WarehouseInputDto
{
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Required, StringLength(30)] public string Code { get; set; } = "";
    [StringLength(500)] public string? Address { get; set; }
     public Guid? BranchId { get; set; }
     public bool IsDefault { get; set; }
     public bool IsActive { get; set; } = true;
}
public sealed class WarehouseDto : WarehouseInputDto
{
    public Guid Id { get; set; }

}
