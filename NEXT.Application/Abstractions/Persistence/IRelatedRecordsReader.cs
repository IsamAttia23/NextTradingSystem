using NEXT.Application.DTOs.Presentation;

namespace NEXT.Application.Abstractions.Persistence;

public interface IRelatedRecordsReader
{
    Task<ActivityPageDto> ReadAsync<TEntity>(Guid id, string? kind, int page, CancellationToken ct) where TEntity : class;
}
