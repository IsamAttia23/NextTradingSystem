using Microsoft.EntityFrameworkCore;
using NEXT.Domain.Common.Entities;
using NEXT.Application.Abstractions.Persistence;
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

        public Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
            => GetByKeyAsync([id], cancellationToken);

        public virtual Task DeleteAsync(TEntity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            // SaveChanges converts deletions to soft deletes and records the recycle-bin snapshot.
            _set.Remove(entity);
            return Task.CompletedTask;
        }
        public virtual async Task<bool> IsExistsAsync(TKey id)
        {
            return await _set.AnyAsync(x => x.Id.Equals(id));
        }
    }
}
