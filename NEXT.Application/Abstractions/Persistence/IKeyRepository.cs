namespace NEXT.Application.Abstractions.Persistence;

public interface IKeyRepository<TEntity, TKey> : IRepository<TEntity>
    where TEntity : class
    where TKey : struct
{
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
    Task DeleteAsync(TEntity entity);
    Task<bool> IsExistsAsync(TKey id);
}
