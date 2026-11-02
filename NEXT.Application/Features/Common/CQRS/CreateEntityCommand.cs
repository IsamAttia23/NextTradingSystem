using MediatR;

namespace NEXT.Application.Features.Common.CQRS;

/// <summary>Returns the saved entity snapshot. Use a read query when enriched relationship fields are required.</summary>
public sealed record CreateEntityCommand<TDto, TInput>(TInput Data) : IRequest<TDto>
    where TDto : class where TInput : class;
