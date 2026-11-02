using NEXT.Application.Validation;
using Mapster;
using MediatR;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.Exceptions;
using NEXT.Domain.Common.Entities;

namespace NEXT.Application.Features.Common.CQRS;

public sealed class GetEntityActivityQueryHandler<TEntity, TDto>(IKeyRepository<TEntity, Guid> repository, IRelatedRecordsReader related)
    : IRequestHandler<GetEntityActivityQuery<TDto>, ActivityPageDto> where TEntity : AuditableEntity<Guid> where TDto : class
{
    public async Task<ActivityPageDto> Handle(GetEntityActivityQuery<TDto> request, CancellationToken ct)
    {
        RequestValidation.Identifier(request.Id);
        _ = await repository.GetByIdAsync(request.Id, ct) ?? throw new NotFoundException("Record was not found.");
        return await related.ReadAsync<TEntity>(request.Id, request.Kind, request.Page, ct);
    }
}
