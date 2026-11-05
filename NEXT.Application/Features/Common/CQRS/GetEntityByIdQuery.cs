using MediatR;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Presentation;

namespace NEXT.Application.Features.Common.CQRS;

public sealed record GetEntityByIdQuery<TDto>(Guid Id, bool ForEdit = false) : IRequest<TDto> where TDto : class;
