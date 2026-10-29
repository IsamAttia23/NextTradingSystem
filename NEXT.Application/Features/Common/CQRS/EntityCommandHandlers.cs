using MapsterMapper;
using MediatR;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Media;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Exceptions;
using NEXT.Application.Services;
using NEXT.Application.Validation;
using NEXT.Domain.Common.Entities;

namespace NEXT.Application.Features.Common.CQRS;

public sealed class CreateEntityCommandHandler<TEntity, TDto, TCreationDto>(
    IKeyRepository<TEntity, Guid> repository, IMapper mapper, IUnitOfWork unitOfWork,
    IEntityValidator<TCreationDto> validator, IEnumerable<IEntityRules<TEntity, TCreationDto>> rules,
    IProfileImageProcessor? images = null)
    : IRequestHandler<CreateEntityCommand<TDto, TCreationDto>, TDto>
    where TEntity : AuditableEntity<Guid> where TDto : class where TCreationDto : class
{
    public async Task<TDto> Handle(CreateEntityCommand<TDto, TCreationDto> request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        validator.Validate(request.Data);
        foreach (var rule in rules) await rule.ValidateAsync(null, request.Data, ct);
        var entity = mapper.Map<TEntity>(request.Data);
        entity.Id = Guid.NewGuid();
        EntityInput.SetImage(entity, request.Data, images);
        await repository.AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return mapper.Map<TDto>(entity);
    }
}

public sealed class UpdateEntityCommandHandler<TEntity, TDto, TUpdateDto>(
    IKeyRepository<TEntity, Guid> repository, IMapper mapper, IUnitOfWork unitOfWork,
    IEntityValidator<TUpdateDto> validator, IEnumerable<IEntityRules<TEntity, TUpdateDto>> rules,
    IProfileImageProcessor? images = null)
    : IRequestHandler<UpdateEntityCommand<TDto, TUpdateDto>, TDto>
    where TEntity : AuditableEntity<Guid> where TDto : class where TUpdateDto : class
{
    public async Task<TDto> Handle(UpdateEntityCommand<TDto, TUpdateDto> request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (request.Data is TIdType<Guid> keyed) keyed.Id = request.Id;
        validator.Validate(request.Data);
        var entity = await repository.GetByIdAsync(request.Id, ct) ?? throw new NotFoundException("Record was not found.");
        foreach (var rule in rules) await rule.ValidateAsync(request.Id, request.Data, ct);
        var existing = (entity as IHasProfileImage)?.AvatarUrl;
        mapper.Map(request.Data, entity);
        entity.Id = request.Id;
        EntityInput.SetImage(entity, request.Data, images, existing);
        await unitOfWork.SaveChangesAsync(ct);
        return mapper.Map<TDto>(entity);
    }
}

public sealed class DeleteEntityCommandHandler<TEntity, TDto>(
    IKeyRepository<TEntity, Guid> repository, IUnitOfWork unitOfWork, IDeletionAuditContext? deletionAudit = null)
    : IRequestHandler<DeleteEntityCommand<TDto>>
    where TEntity : AuditableEntity<Guid> where TDto : class
{
    public async Task Handle(DeleteEntityCommand<TDto> request, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(request.Id, ct) ?? throw new NotFoundException("Record was not found.");
        if (await repository.HasActiveRelationsAsync(entity))
            throw new UnprocessableEntityException("This record is in use and cannot be deleted.");
        var reason = DeletionReason.Require(request.Reason);
        using var audit = deletionAudit?.Begin(reason);
        if (entity is IIsDeletableEntity deleted) deleted.DeletionReason = reason;
        await repository.DeleteAsync(entity);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
