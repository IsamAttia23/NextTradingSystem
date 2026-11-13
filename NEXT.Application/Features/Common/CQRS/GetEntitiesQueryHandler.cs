using Mapster;
using MediatR;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.Validation;
using NEXT.Domain.Common.Entities;

namespace NEXT.Application.Features.Common.CQRS;

public sealed class GetEntitiesQueryHandler<TEntity, TDto>(
    IReadRepository<TEntity> repository, IPaginationServices pagination,
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
        var entities = repository.Query(repository.BuildFilterExpression(query));
        var ordered = repository.ApplySorting(entities, query.SortBy, query.SortDirection);
        return await pagination.GetPagedList(ordered, source => source.ProjectToType<TDto>(mapping), query, ct);
    }
}
