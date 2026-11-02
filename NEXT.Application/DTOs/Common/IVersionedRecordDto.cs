namespace NEXT.Application.DTOs.Common;

public interface IVersionedRecordDto : IRecordDto
{
    byte[] RowVersion { get; }
}
