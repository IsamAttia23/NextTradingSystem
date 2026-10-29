using MediatR;
namespace NEXT.Application.Features.Common.CQRS;

public sealed record CreateEntityCommand<TDto, TInput>(TInput Data) : IRequest<TDto>
    where TDto : class where TInput : class;
public sealed record UpdateEntityCommand<TDto, TInput>(Guid Id, TInput Data) : IRequest<TDto>
    where TDto : class where TInput : class;
public sealed record DeleteEntityCommand<TDto>(Guid Id, string? Reason = null) : IRequest where TDto : class;
