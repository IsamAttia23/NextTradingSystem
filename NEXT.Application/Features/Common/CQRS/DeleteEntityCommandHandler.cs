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

public sealed class DeleteEntityCommandHandler<TEntity, TDto>(
    IKeyRepository<TEntity, Guid> repository, IUnitOfWork unitOfWork, IDeletionAuditContext? deletionAudit = null)
    : IRequestHandler<DeleteEntityCommand<TDto>>
    where TEntity : AuditableEntity<Guid> where TDto : class
{
    public async Task Handle(DeleteEntityCommand<TDto> request, CancellationToken ct)
    {
        RequestValidation.Identifier(request.Id);
        var reason = DeletionReason.Require(request.Reason);
        var entity = await repository.GetByIdAsync(request.Id, ct) ?? throw new NotFoundException("Record was not found.");
        if (await repository.HasActiveRelationsAsync(entity, ct))
        {
            throw new RecordRelationshipException();
        }
        using var audit = deletionAudit?.Begin(reason);
        if (entity is IIsDeletableEntity deleted)
        {
            deleted.DeletionReason = reason;
        }
        await repository.DeleteAsync(entity);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
