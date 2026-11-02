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
        if (request.Data is null) RequestValidation.Fail("Data", "Input is required.");
        foreach (var rule in rules) rule.Normalize(request.Data);
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
