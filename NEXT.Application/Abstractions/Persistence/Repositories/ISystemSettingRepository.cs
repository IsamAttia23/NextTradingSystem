using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for SystemSetting. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface ISystemSettingRepository : IRepository<SystemSetting>
{
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    IQueryable<SystemSetting> GetByCategoryAsync(string category);
}
