using NEXT.Application.DTOs.Common;
namespace NEXT.Application.DTOs.Customers;

public sealed class CustomerDto : CustomerInputDto, ITaggedRecordDto
{
    public string? TagName { get; set; }
    public string? TagColor { get; set; }
    public Guid Id { get; set; }
    public decimal CurrentBalance { get; set; }
}
