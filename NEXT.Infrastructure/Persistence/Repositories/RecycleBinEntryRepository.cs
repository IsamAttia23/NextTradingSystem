using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for RecycleBinEntry; inherits common CRUD behavior from the generic repository.</summary>
public sealed class RecycleBinEntryRepository(NEXTDbContext context) : TRepository<RecycleBinEntry>(context), IRecycleBinEntryRepository
{
    public Task<List<RecycleBinEntry>> GetActiveAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        => context.RecycleBinEntries.AsNoTracking().Where(x=>x.ExpiresAt==null||x.ExpiresAt>now).OrderByDescending(x=>x.DeletedAt).ToListAsync(cancellationToken);
}
