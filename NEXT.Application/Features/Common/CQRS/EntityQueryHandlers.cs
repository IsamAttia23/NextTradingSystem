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
    IKeyRepository<TEntity, Guid> repository, IPaginationServices pagination)
    : IRequestHandler<GetEntityByIdQuery<TDto>, TDto?>
    where TEntity : AuditableEntity<Guid> where TDto : class
{
    public async Task<TDto?> Handle(GetEntityByIdQuery<TDto> request, CancellationToken ct)
    {
        var result = await pagination.GetPaggedList(repository.Query().Where(x => x.Id == request.Id).ProjectToType<TDto>(),
            new PaginationQuery { PageSize = 1 }, ct);
        return result.Data.SingleOrDefault() ?? throw new NotFoundException("Record was not found.");
    }
}

public sealed class GetEntitiesQueryHandler<TEntity, TDto>(
    IKeyRepository<TEntity, Guid> repository, IPaginationServices pagination)
    : IRequestHandler<GetEntitiesQuery<TDto>, TPaginationQueryModel<TDto>>
    where TEntity : AuditableEntity<Guid> where TDto : class
{
    public Task<TPaginationQueryModel<TDto>> Handle(GetEntitiesQuery<TDto> request, CancellationToken ct) =>
        pagination.GetPaggedList(repository.Search(request.Pagination).ProjectToType<TDto>(), request.Pagination, ct);
}

public sealed class GetEntityActivityQueryHandler<TEntity, TDto>(IKeyRepository<TEntity, Guid> repository, IRelatedRecordsReader related)
    : IRequestHandler<GetEntityActivityQuery<TDto>, ActivityPageDto> where TEntity : AuditableEntity<Guid> where TDto : class
{
    public async Task<ActivityPageDto> Handle(GetEntityActivityQuery<TDto> request, CancellationToken ct)
    {
        _ = await repository.GetByIdAsync(request.Id, ct) ?? throw new NotFoundException("Record was not found.");
        return await related.ReadAsync<TEntity>(request.Id, request.Kind, request.Page, ct);
    }
}
