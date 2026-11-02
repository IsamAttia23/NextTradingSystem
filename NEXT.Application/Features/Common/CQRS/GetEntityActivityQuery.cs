using MediatR;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Presentation;

namespace NEXT.Application.Features.Common.CQRS;

public sealed record GetEntityActivityQuery<TDto>(Guid Id, string? Kind, int Page) : IRequest<ActivityPageDto> where TDto : class;
