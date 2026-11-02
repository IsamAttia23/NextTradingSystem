namespace NEXT.Application.DTOs.Products;

public sealed class ProductDto : ProductInputDto, NEXT.Application.DTOs.Common.IVersionedRecordDto
{
    public byte[] RowVersion { get; set; } = [];

    public Guid Id { get; set; }

}
