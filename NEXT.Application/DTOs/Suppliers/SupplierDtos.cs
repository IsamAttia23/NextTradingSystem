using NEXT.Application.DTOs.Common;
using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Suppliers;
public class SupplierInputDto : IProfileImageInput
{
    [StringLength(40)] public string? Code { get; set; }
    public byte[]? AvatarUrl { get; set; }
    public bool RemoveAvatar { get; set; }
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [StringLength(100)] public string? TaxNumber { get; set; }
    [Phone, StringLength(32)] public string? Phone { get; set; }
    [EmailAddress, StringLength(256)] public string? Email { get; set; }
    [StringLength(500)] public string? Address { get; set; }
    [Range(0,3650)] public int PaymentTermsDays { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
     public bool IsActive { get; set; } = true;
    [Required] public Guid TagId { get; set; }
}
public sealed class SupplierDto : SupplierInputDto, ITaggedRecordDto
{
    public string? TagName { get; set; }
    public string? TagColor { get; set; }
    public Guid Id { get; set; }
    public decimal CurrentBalance { get; set; }
}
