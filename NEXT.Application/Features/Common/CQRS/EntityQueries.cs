using MediatR;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Presentation;
namespace NEXT.Application.Features.Common.CQRS;

public sealed record GetEntityByIdQuery<TDto>(Guid Id) : IRequest<TDto?> where TDto : class;
public sealed record GetEntitiesQuery<TDto>(PaginationQuery Pagination) : IRequest<TPaginationQueryModel<TDto>> where TDto : class;
public sealed record GetEntityActivityQuery<TDto>(Guid Id, string? Kind, int Page) : IRequest<ActivityPageDto> where TDto : class;
