namespace NEXT.Application.DTOs.Branches;

public sealed class BranchDto : BranchInputDto, NEXT.Application.DTOs.Common.IRecordDto
{
    public Guid Id { get; set; }

}
