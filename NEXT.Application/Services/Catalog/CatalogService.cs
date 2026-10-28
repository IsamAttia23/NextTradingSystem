using Mapster;
using MapsterMapper;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Common.Entities;
namespace NEXT.Application.Services.Catalog;
public interface ICatalogRules<TEntity, in TInput>
{
    Task ValidateAsync(Guid? id, TInput input, CancellationToken ct);
}
public sealed class CatalogService<TEntity, TDto, TInput>(IKeyRepository<TEntity, Guid> repository,
    IMapper mapper, IPaginationServices pagination, IUnitOfWork unitOfWork,
    IEntityValidator<TInput> validator, IEnumerable<ICatalogRules<TEntity, TInput>> rules,
    IRelatedRecordsReader? related = null, NEXT.Application.Abstractions.Media.IProfileImageProcessor? images = null, NEXT.Application.Abstractions.Context.IDeletionAuditContext? deletionAudit = null)
    : ICatalogService<TDto, TInput>
    where TEntity : AuditableEntity<Guid> where TDto : class where TInput : class
{
    public Task<TPaginationQueryModel<TDto>> ListAsync(PaginationQuery query, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return pagination.GetPaggedList(CatalogQueryPipeline.Apply(repository.Query(), query).ProjectToType<TDto>(), query, ct);
    }
    public async Task<TDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var result = await pagination.GetPaggedList(repository.Query().Where(x => x.Id == id).ProjectToType<TDto>(), new PaginationQuery { PageSize = 1 }, ct);
        return result.Data.SingleOrDefault() ?? throw new NotFoundException("Record was not found.");
    }
    public async Task<NEXT.Application.DTOs.Presentation.ActivityPageDto> ActivityAsync(Guid id, string? kind, int page, CancellationToken ct = default)
    {
        await Required(id, ct);
        return related is null ? new([], null, [], 1, 0, 0) : await related.ReadAsync<TEntity>(id, kind, page, ct);
    }
    public async Task<TDto> CreateAsync(TInput input, CancellationToken ct = default)
    {
        await Validate(null, input, ct);
        var entity = mapper.Map<TEntity>(input);
        entity.Id = Guid.NewGuid();
        SetImage(entity, input);
        await repository.AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return mapper.Map<TDto>(entity);
    }
    public async Task<TDto> UpdateAsync(Guid id, TInput input, CancellationToken ct = default)
    {
        var entity = await Required(id, ct);
        await Validate(id, input, ct);
        var existingImage = (entity as IHasProfileImage)?.AvatarUrl;
        mapper.Map(input, entity);
        SetImage(entity, input, existingImage);
        await unitOfWork.SaveChangesAsync(ct);
        return mapper.Map<TDto>(entity);
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct = default, string? reason = null)
    {
        var entity = await Required(id, ct);
        if (await repository.HasActiveRelationsAsync(entity))
            throw new UnprocessableEntityException("This record is in use and cannot be deleted.");
        var normalized = NEXT.Application.Services.DeletionReason.Require(reason);
        using var audit = deletionAudit?.Begin(normalized);
        if (entity is IIsDeletableEntity deleted) deleted.DeletionReason = normalized;
        await repository.DeleteAsync(entity);
        await unitOfWork.SaveChangesAsync(ct);
    }
    private void SetImage(TEntity entity, TInput input, byte[]? existing = null)
    {
        if (entity is not IHasProfileImage target || input is not NEXT.Application.DTOs.Catalog.IProfileImageInput source) return;
        if (source.RemoveAvatar) { target.AvatarUrl = null; return; }
        target.AvatarUrl = source.AvatarUrl is { Length: > 0 }
            ? (images ?? throw new InvalidOperationException("Image processor is not registered.")).Normalize(source.AvatarUrl) : existing;
    }
    private async Task Validate(Guid? id, TInput input, CancellationToken ct)
    {
        validator.Validate(input);
        foreach (var rule in rules) await rule.ValidateAsync(id, input, ct);
    }
    private async Task<TEntity> Required(Guid id, CancellationToken ct) =>
        await repository.GetByIdAsync(id, ct) ?? throw new NotFoundException("Record was not found.");
}
