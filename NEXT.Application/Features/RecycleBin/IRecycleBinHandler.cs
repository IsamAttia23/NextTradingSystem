using NEXT.Domain.Common.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace NEXT.Application.Features.RecycleBin;

public interface IRecycleBinHandler<TEntity, TKey>
        where TKey : struct
        where TEntity : IsDeletableEntity<TKey>
    {
        string EntityKey { get; }

        Task OnRestoreAsync(TEntity entity, CancellationToken ct = default);
        Task OnHardDeleteAsync(TEntity entity, CancellationToken ct = default);
    }
