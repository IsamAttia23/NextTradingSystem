using MediatR;

namespace NEXT.Application.Features.Common.CQRS;

/// <summary>The envelope Id is authoritative. Versioned entities require the token returned by their read DTO.</summary>
public sealed record UpdateEntityCommand<TDto, TInput>(Guid Id, TInput Data, byte[]? RowVersion = null) : IRequest<TDto>
    where TDto : class where TInput : class;
