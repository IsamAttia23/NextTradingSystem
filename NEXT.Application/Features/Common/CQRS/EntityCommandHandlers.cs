using MediatR;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Validation;
using NEXT.Domain.Common.Entities;

namespace NEXT.Application.Features.Common.CQRS;

/// <summary>Generic handlers delegate to TEntityService and share input validation.</summary>
public sealed class CreateEntityCommandHandler<TId, TDto, TCreationDto, TUpdateDto>(
    IEntityServices<TId, TDto, TCreationDto, TUpdateDto> service,
    IEntityValidator<TCreationDto> validator)
    : IRequestHandler<CreateEntityCommand<TId, TDto, TCreationDto, TUpdateDto>, TDto>
    where TId : struct
    where TDto : class, TIdType<TId>
    where TCreationDto : class
    where TUpdateDto : class, TIdType<TId>
{
    public Task<TDto> Handle(CreateEntityCommand<TId, TDto, TCreationDto, TUpdateDto> request, CancellationToken cancellationToken)
    {
        validator.Validate(request.Data);
        return service.Add(request.Data);
    }
}

public sealed class UpdateEntityCommandHandler<TId, TDto, TCreationDto, TUpdateDto>(
    IEntityServices<TId, TDto, TCreationDto, TUpdateDto> service,
    IEntityValidator<TUpdateDto> validator)
    : IRequestHandler<UpdateEntityCommand<TId, TDto, TCreationDto, TUpdateDto>, TDto>
    where TId : struct
    where TDto : class, TIdType<TId>
    where TCreationDto : class
    where TUpdateDto : class, TIdType<TId>
{
    public Task<TDto> Handle(UpdateEntityCommand<TId, TDto, TCreationDto, TUpdateDto> request, CancellationToken cancellationToken)
    {
        request.Data.Id = request.Id;
        validator.Validate(request.Data);
        return service.Update(request.Id, request.Data);
    }
}

public sealed class DeleteEntityCommandHandler<TId, TDto, TCreationDto, TUpdateDto>(
    IEntityServices<TId, TDto, TCreationDto, TUpdateDto> service)
    : IRequestHandler<DeleteEntityCommand<TId, TDto, TCreationDto, TUpdateDto>>
    where TId : struct
    where TDto : class, TIdType<TId>
    where TCreationDto : class
    where TUpdateDto : class, TIdType<TId>
{
    public Task Handle(DeleteEntityCommand<TId, TDto, TCreationDto, TUpdateDto> request, CancellationToken cancellationToken) =>
        service.Delete(request.Id);
}
