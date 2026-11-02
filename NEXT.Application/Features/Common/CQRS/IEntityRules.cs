namespace NEXT.Application.Features.Common.CQRS;

public interface IEntityRules<TEntity, in TInput>
{
    void Normalize(TInput input) { }
    Task ValidateAsync(Guid? id, TInput input, CancellationToken ct);
}
