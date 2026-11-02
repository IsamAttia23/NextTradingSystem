using MediatR;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Presentation;

namespace NEXT.Application.Features.Common.CQRS;

public sealed record GetEntitiesQuery<TDto>(PaginationQuery Pagination) : IRequest<TPaginationQueryModel<TDto>> where TDto : class;
