using MediatR;
using NEXT.Application.Abstractions.IServices;
using NEXT.Domain.Common.Entities;
using NEXT.Domain.Common.QueryModels;

namespace NEXT.Application.Features.Common.CQRS;

public sealed class GetEntityByIdQueryHandler<TId, TDto, TCreationDto, TUpdateDto>(
    IEntityServices<TId, TDto, TCreationDto, TUpdateDto> service)
    : IRequestHandler<GetEntityByIdQuery<TId, TDto, TCreationDto, TUpdateDto>, TDto?>
    where TId : struct
    where TDto : class, TIdType<TId>
    where TCreationDto : class
    where TUpdateDto : class, TIdType<TId>
{
    public Task<TDto?> Handle(GetEntityByIdQuery<TId, TDto, TCreationDto, TUpdateDto> request, CancellationToken cancellationToken) =>
        service.Details(request.Id);
}

public sealed class GetEntitiesQueryHandler<TId, TDto, TCreationDto, TUpdateDto>(
    IEntityServices<TId, TDto, TCreationDto, TUpdateDto> service)
    : IRequestHandler<GetEntitiesQuery<TId, TDto, TCreationDto, TUpdateDto>, TPaginationQueryModel<TDto>>
    where TId : struct
    where TDto : class, TIdType<TId>
    where TCreationDto : class
    where TUpdateDto : class, TIdType<TId>
{
    public Task<TPaginationQueryModel<TDto>> Handle(GetEntitiesQuery<TId, TDto, TCreationDto, TUpdateDto> request, CancellationToken cancellationToken) =>
        service.GetAll(request.Pagination);
}
