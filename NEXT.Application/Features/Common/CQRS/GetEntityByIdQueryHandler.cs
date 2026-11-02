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

public sealed class GetEntityByIdQueryHandler<TEntity, TDto>(
    IKeyRepository<TEntity, Guid> repository, IQueryExecutor executor, TypeAdapterConfig? mapping = null)
    : IRequestHandler<GetEntityByIdQuery<TDto>, TDto>
    where TEntity : AuditableEntity<Guid> where TDto : class
{
    public async Task<TDto> Handle(GetEntityByIdQuery<TDto> request, CancellationToken ct)
    {
        RequestValidation.Identifier(request.Id);
        var entities = repository.FindAsync(x => x.Id == request.Id, ct);
        return await executor.FirstOrDefaultAsync(entities.ProjectToType<TDto>(mapping ?? new TypeAdapterConfig()), ct)
            ?? throw new NotFoundException("Record was not found.");
    }
}
