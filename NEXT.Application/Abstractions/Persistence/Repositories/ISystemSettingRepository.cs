using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

public interface ISystemSettingRepository : IRepository<SystemSetting>
{
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    IQueryable<SystemSetting> GetByCategoryAsync(string category);
}
