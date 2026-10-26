using Microsoft.EntityFrameworkCore;
using NEXT.Domain.Common.Entities;
using NEXT.Infrastructure.Persistence.IRepositories;
using System;
using System.Collections.Generic;
using System.Text;

namespace NEXT.Infrastructure.Persistence.Repositories
{
    public class KeyRepository<TEntity,TKey>(NEXTDbContext context) : TRepository<TEntity>(context) , IKeyRepository<TEntity, TKey>
        where TEntity : AuditableEntity<TKey>
        where TKey : struct
    {
        private readonly DbSet<TEntity> _set = context.Set<TEntity>();
        
        public async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
        {
            object?[] keys = id is object?[] compositeKeys ? compositeKeys : [id];
            return await _set.FindAsync(keys, cancellationToken);
        }

        public virtual async Task DeleteAsync(TEntity entity)
        {
            if (entity is IsDeletableEntity<TKey> deletableEntity)
            {
                deletableEntity.DeletedAt = DateTime.UtcNow;

                _set.Update(entity);
            }
        }

        public virtual async Task<bool> IsExistsAsync(TKey id)
        {
            return await _set.AnyAsync(x => x.Id.Equals(id));
        }
    }
}
