using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NEXT.Application.Abstractions.Services;
using NEXT.Domain.Common.Entities;
using NEXT.Domain.Common.QueryModels;
using NEXT.Infrastructure.Persistence;
using System;
using System.Collections.Generic;

namespace NEXT.Application.Features.RecycleBin
{
    public class RecycleBinService<TEntity, TKey>
        where TKey : struct
        where TEntity : IsDeletableEntity<TKey>
    {
        private readonly NEXTDbContext _db;
        private readonly IServiceProvider _sp;
        private readonly Dictionary<string, IEntityType> _supported;
        public IEnumerable<string> Keys => _supported.Keys;
        private readonly IPaginationServices _paginationServices;

        public RecycleBinService(NEXTDbContext db, IServiceProvider sp, IPaginationServices paginationServices, IModel model)
        {
            _db = db;
            _sp = sp;
            _paginationServices = paginationServices;
            _supported = model.GetEntityTypes()
            .Where(e => !e.ClrType.IsAbstract &&
                        typeof(IsDeletableEntity<TKey>).IsAssignableFrom(e.ClrType))
            .ToDictionary(e => e.ClrType.Name, e => e, StringComparer.OrdinalIgnoreCase);
        }

        public bool IsSupported(string key) => _supported.ContainsKey(key.ToLowerInvariant());

        public IEntityType Resolve(string key) =>
         _supported.TryGetValue(key, out var type) ? type
             : throw new ArgumentException($"Unsupported entity type '{key}'. Entity must implement ISoftDeletable.",
                 nameof(key));

        public async Task<TPaginationQueryModel<RecycleBinItemDto>> List(string entityType, PaginationQuery query, CancellationToken ct = default)
        {
            if (!IsSupported(entityType)) throw new ArgumentException("Unsupported entity type", nameof(entityType));
            var clrType = ResolveClrType(entityType);
            var method = typeof(DbContext).GetMethod("Set", Type.EmptyTypes)!.MakeGenericMethod(clrType);
            var set = (IQueryable)method.Invoke(_db, null)!;

            // deleted items: DeletedOn != null
            var deleted = set.Cast<TEntity>().Where(x => x.DeletedAt != null);

            var projected = deleted.Select(x => new RecycleBinItemDto
            {
                EntityType = entityType.ToLowerInvariant(),
                EntityId = Convert.ToInt32(x.Id),
                DeletedOn = x.DeletedAt!.Value
            }).AsQueryable();
            var pagged = await _paginationServices.GetPaggedList(projected, query);
            return pagged;
        }

        public async Task Restore(string entityType, int id, CancellationToken ct = default)
        {
            if (!IsSupported(entityType)) throw new ArgumentException("Unsupported entity type", nameof(entityType));

            var clrType = ResolveClrType(entityType);
            var method = typeof(DbContext).GetMethod("Set", Type.EmptyTypes)!.MakeGenericMethod(clrType);
            var set = (IQueryable)method.Invoke(_db, null)!;

            var entity = await set.Cast<TEntity>().FirstOrDefaultAsync(x => x.Id.Equals(id), ct);
            if (entity == null) throw new KeyNotFoundException("Entity not found");
            if (entity.DeletedAt == null) throw new InvalidOperationException("Entity is not deleted");

            // call handler if any
            var handler = GetHandler(entityType);
            if (handler != null)
            {
                await handler.OnRestoreAsync(entity, ct);
            }

            entity.DeletedAt = null;
            await _db.SaveChangesAsync(ct);
        }

        public async Task HardDelete(string entityType, int id, CancellationToken ct = default)
        {
            if (!IsSupported(entityType)) throw new ArgumentException("Unsupported entity type", nameof(entityType));

            var clrType = ResolveClrType(entityType);
            var method = typeof(DbContext).GetMethod("Set", Type.EmptyTypes)!.MakeGenericMethod(clrType);
            var set = (IQueryable)method.Invoke(_db, null)!;

            var entity = await set.Cast<TEntity>().FirstOrDefaultAsync(x => x.Id.Equals(id), ct);
            if (entity == null) throw new KeyNotFoundException("Entity not found");

            var handler = GetHandler(entityType);
            if (handler != null)
            {
                await handler.OnHardDeleteAsync(entity, ct);
            }

            _db.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }

        private IRecycleBinHandler<TEntity, TKey>? GetHandler(string entityType)
        {
            // resolve handler by matching EntityKey
            var handlers = _sp.GetService(typeof(IEnumerable<IRecycleBinHandler<TEntity, TKey>>)) as IEnumerable<IRecycleBinHandler<TEntity, TKey>>;
            return handlers?.FirstOrDefault(h => string.Equals(h.EntityKey, entityType, StringComparison.OrdinalIgnoreCase));
        }

        private Type ResolveClrType(string entityType)
        {
            return Resolve(entityType).ClrType;
        }
    }
}
