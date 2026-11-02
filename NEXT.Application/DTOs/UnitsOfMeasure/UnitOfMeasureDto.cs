namespace NEXT.Application.DTOs.UnitsOfMeasure;

public sealed class UnitOfMeasureDto : UnitOfMeasureInputDto, NEXT.Application.DTOs.Common.IRecordDto
{
    public Guid Id { get; set; }

}
