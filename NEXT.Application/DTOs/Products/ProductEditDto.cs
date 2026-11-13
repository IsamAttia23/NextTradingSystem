using NEXT.Application.DTOs.Common;

namespace NEXT.Application.DTOs.Products;

public sealed class ProductEditDto : ProductInputDto, IVersionedRecordDto
{
    public Guid Id { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
