using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for SystemSetting. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface ISystemSettingRepository : IRepository<SystemSetting, string>
{
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    IQueryable<SystemSetting> GetByCategoryAsync(string category);
}
