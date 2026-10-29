namespace NEXT.Application.Features.Common.CQRS;

public interface IEntityRules<TEntity, in TInput>
{
    Task ValidateAsync(Guid? id, TInput input, CancellationToken ct);
}
