namespace NEXT.Application.DTOs.Warehouses;

public sealed class WarehouseDto : WarehouseInputDto, NEXT.Application.DTOs.Common.IRecordDto
{
    public Guid Id { get; set; }

}
