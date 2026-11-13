using Mapster;
using MediatR;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Common.Entities;

namespace NEXT.Application.Features.Common.CQRS;

public sealed class GetEntityByIdQueryHandler<TEntity, TDto>(
    IReadRepository<TEntity> repository, IQueryExecutor executor, TypeAdapterConfig? mapping = null)
    : IRequestHandler<GetEntityByIdQuery<TDto>, TDto>
    where TEntity : AuditableEntity<Guid> where TDto : class
{
    public Task<TDto> Handle(GetEntityByIdQuery<TDto> request, CancellationToken ct)
    {
        RequestValidation.Identifier(request.Id);
        return ReadAsync(repository, executor, request.Id, mapping ?? new TypeAdapterConfig(), ct);
    }

    internal static async Task<TDto> ReadAsync(IReadRepository<TEntity> repository, IQueryExecutor executor,
        Guid id, TypeAdapterConfig mapping, CancellationToken ct)
    {
        var entities = repository.Query(x => x.Id == id);
        return await executor.FirstOrDefaultAsync(entities.ProjectToType<TDto>(mapping), ct)
            ?? throw new NotFoundException("Record was not found.");
    }
}
