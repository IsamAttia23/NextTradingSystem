using MediatR;
using NEXT.Domain.Common.Entities;
using NEXT.Application.Common.QueryModels;

namespace NEXT.Application.Features.Common.CQRS;

/// <summary>Reusable CQRS read requests for entities handled by TEntityService.</summary>
public sealed record GetEntityByIdQuery<TId, TDto, TCreationDto, TUpdateDto>(TId Id)
    : IRequest<TDto?>
    where TId : struct
    where TDto : class, TIdType<TId>
    where TCreationDto : class
    where TUpdateDto : class, TIdType<TId>;

public sealed record GetEntitiesQuery<TId, TDto, TCreationDto, TUpdateDto>(PaginationQuery Pagination)
    : IRequest<TPaginationQueryModel<TDto>>
    where TId : struct
    where TDto : class, TIdType<TId>
    where TCreationDto : class
    where TUpdateDto : class, TIdType<TId>;
