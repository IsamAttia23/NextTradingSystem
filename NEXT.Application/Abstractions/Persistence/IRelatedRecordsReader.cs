using NEXT.Application.DTOs.Presentation;
namespace NEXT.Application.Abstractions.Persistence;
/// <summary>Read-only projections of direct and indirect records related to a catalog entity.</summary>
public interface IRelatedRecordsReader
{
    Task<ActivityPageDto> ReadAsync<TEntity>(Guid id, string? kind, int page, CancellationToken ct) where TEntity : class;
}
