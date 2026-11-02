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
        if (request.Data is null) RequestValidation.Fail("Data", "Input is required.");
        RequestValidation.Identifier(request.Id);
        if (request.Data is TIdType<Guid> keyed) keyed.Id = request.Id;
        foreach (var rule in rules) rule.Normalize(request.Data);
        validator.Validate(request.Data);
        var entity = await repository.GetByIdAsync(request.Id, ct) ?? throw new NotFoundException("Record was not found.");
        repository.SetRowVersion(entity, request.RowVersion);
        foreach (var rule in rules) await rule.ValidateAsync(request.Id, request.Data, ct);
        var existing = (entity as IHasProfileImage)?.AvatarUrl;
        mapper.Map(request.Data, entity);
        entity.Id = request.Id;
        await EntityInput.SetImageAsync(entity, request.Data, images, ct, existing);
        await unitOfWork.SaveChangesAsync(ct);
        return mapper.Map<TDto>(entity);
    }
}
