using MediatR;

namespace NEXT.Application.Features.Common.CQRS;

public sealed record DeleteEntityCommand<TDto>(Guid Id, string? Reason = null) : IRequest where TDto : class;
