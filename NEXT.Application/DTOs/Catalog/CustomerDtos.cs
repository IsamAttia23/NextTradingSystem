using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Catalog;
public class CustomerInputDto : IProfileImageInput
{
    [EnumDataType(typeof(CustomerType))] public CustomerType Type { get; set; } = CustomerType.Individual;
    public byte[]? AvatarUrl { get; set; }
    public bool RemoveAvatar { get; set; }
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [StringLength(200)] public string? CompanyName { get; set; }
    [StringLength(100)] public string? TaxNumber { get; set; }
    [StringLength(100)] public string? CommercialRegister { get; set; }
    [EnumDataType(typeof(CustomerCategory))] public CustomerCategory Category { get; set; } = CustomerCategory.Retail;
    [Phone, StringLength(32)] public string? Phone { get; set; }
    [EmailAddress, StringLength(256)] public string? Email { get; set; }
    [StringLength(500)] public string? Address { get; set; }
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal CreditLimit { get; set; }
    [Range(0,3650)] public int PaymentTermsDays { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
     public bool IsActive { get; set; } = true;
    [Required] public Guid TagId { get; set; }
}
public sealed class CustomerDto : CustomerInputDto, ITaggedRecordDto
{
    public string? TagName { get; set; }
    public string? TagColor { get; set; }
    public Guid Id { get; set; }
    public decimal CurrentBalance { get; set; }
}
