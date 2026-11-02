namespace NEXT.Application.DTOs.Products;

public sealed class ProductDto : ProductInputDto, NEXT.Application.DTOs.Common.IRecordDto
{
    public Guid Id { get; set; }

}
