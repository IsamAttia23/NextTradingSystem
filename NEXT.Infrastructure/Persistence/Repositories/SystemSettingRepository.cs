using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for SystemSetting; inherits common CRUD behavior from the generic repository.</summary>
public sealed class SystemSettingRepository(NEXTDbContext context) : TRepository<SystemSetting>(context), ISystemSettingRepository
{
    public Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        => context.SystemSettings.AsNoTracking().FirstOrDefaultAsync(x=>x.Key==key,cancellationToken);
    public IQueryable<SystemSetting> GetByCategoryAsync(string category)
        => context.SystemSettings.AsNoTracking().Where(x=>x.Category==category).OrderBy(x=>x.Key).AsQueryable();
}
