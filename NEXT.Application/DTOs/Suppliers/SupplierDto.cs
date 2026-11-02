using NEXT.Application.DTOs.Common;
namespace NEXT.Application.DTOs.Suppliers;

public sealed class SupplierDto : SupplierInputDto, ITaggedRecordDto, NEXT.Application.DTOs.Common.IRecordDto
{
    public string? TagName { get; set; }
    public string? TagColor { get; set; }
    public Guid Id { get; set; }
    public decimal CurrentBalance { get; set; }
}
