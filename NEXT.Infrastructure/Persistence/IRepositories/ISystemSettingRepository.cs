using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for SystemSetting. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface ISystemSettingRepository : IRepository<SystemSetting, string>
{
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<List<SystemSetting>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
}
