using MediatR;

namespace NEXT.Application.Features.Common.CQRS;

public sealed record GetEntityByIdQuery<TDto>(Guid Id) : IRequest<TDto> where TDto : class;
