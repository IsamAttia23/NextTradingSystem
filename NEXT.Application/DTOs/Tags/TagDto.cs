namespace NEXT.Application.DTOs.Tags;

public sealed class TagDto : TagInputDto, NEXT.Application.DTOs.Common.IRecordDto
{
    public Guid Id { get; set; }

}
