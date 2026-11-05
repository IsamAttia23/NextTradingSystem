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

public sealed class GetEntitiesQueryHandler<TEntity, TDto>(
    IKeyRepository<TEntity, Guid> repository, IPaginationServices pagination,
    IEntityValidator<PaginationQuery> validator, TypeAdapterConfig mapping)
    : IRequestHandler<GetEntitiesQuery<TDto>, TPaginationQueryModel<TDto>>
    where TEntity : AuditableEntity<Guid> where TDto : class
{
    public async Task<TPaginationQueryModel<TDto>> Handle(GetEntitiesQuery<TDto> request, CancellationToken ct)
    {
        var query = request.Pagination;
        if (query is null)
        {
            RequestValidation.Fail("Pagination", "Query is required.");
        }
        validator.Validate(query);
        var expression = repository.BuildFilterExpression(query);
        var entities = repository.FindAsync(expression, ct);
        var orderedEntities = repository.ApplySorting(entities, query.SortBy, query.SortDirection);
        var pagedList = await pagination.GetPagedList(orderedEntities,
            source => source.ProjectToType<TDto>(mapping), query, ct);
        return pagedList;
    }
}
