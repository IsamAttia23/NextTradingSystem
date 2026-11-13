using MediatR;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Common.Entities;

namespace NEXT.Application.Features.Common.CQRS;

public sealed class GetEntityActivityQueryHandler<TEntity, TDto>(IKeyRepository<TEntity, Guid> repository,
    IRelatedRecordsReader related, IActivityPageEnricher? enricher = null)
    : IRequestHandler<GetEntityActivityQuery<TDto>, ActivityPageDto> where TEntity : AuditableEntity<Guid> where TDto : class
{
    public async Task<ActivityPageDto> Handle(GetEntityActivityQuery<TDto> request, CancellationToken ct)
    {
        RequestValidation.Identifier(request.Id);
        _ = await repository.GetByIdAsync(request.Id, ct) ?? throw new NotFoundException("Record was not found.");
        var page = await related.ReadAsync<TEntity>(request.Id, request.Kind, request.Page, ct);
        return enricher is null ? page : await enricher.EnrichAsync(page, ct);
    }
}
