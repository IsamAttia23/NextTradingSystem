namespace NEXT.Application.DTOs.Categories;

public sealed class CategoryDto : CategoryInputDto, NEXT.Application.DTOs.Common.IRecordDto
{
    public Guid Id { get; set; }

}
