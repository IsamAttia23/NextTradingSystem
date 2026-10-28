using MediatR;
using NEXT.Application.Abstractions.IServices;
using NEXT.Domain.Common.Entities;

namespace NEXT.Application.Features.Common.CQRS;

/// <summary>Reusable CQRS write requests for entities handled by TEntityService.</summary>
public sealed record CreateEntityCommand<TId, TDto, TCreationDto, TUpdateDto>(TCreationDto Data)
    : IRequest<TDto>
    where TId : struct
    where TDto : class, TIdType<TId>
    where TCreationDto : class
    where TUpdateDto : class, TIdType<TId>;

public sealed record UpdateEntityCommand<TId, TDto, TCreationDto, TUpdateDto>(TId Id, TUpdateDto Data)
    : IRequest<TDto>
    where TId : struct
    where TDto : class, TIdType<TId>
    where TCreationDto : class
    where TUpdateDto : class, TIdType<TId>;

public sealed record DeleteEntityCommand<TId, TDto, TCreationDto, TUpdateDto>(TId Id, string? Reason = null)
    : IRequest
    where TId : struct
    where TDto : class, TIdType<TId>
    where TCreationDto : class
    where TUpdateDto : class, TIdType<TId>;
