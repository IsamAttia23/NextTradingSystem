using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for JournalEntry; inherits common CRUD behavior from the generic repository.</summary>
public sealed class JournalEntryRepository(NEXTDbContext context) : TRepository<JournalEntry, Guid>(context), IJournalEntryRepository
{
    public Task<JournalEntry?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default) => context.JournalEntries.AsNoTracking().Include(x=>x.Lines).ThenInclude(x=>x.Account).FirstOrDefaultAsync(x=>x.Id==id,cancellationToken);
    public Task<List<JournalEntry>> SearchAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default) { var q=context.JournalEntries.AsNoTracking().AsQueryable(); if(from.HasValue) q=q.Where(x=>x.EntryDate>=from); if(to.HasValue) q=q.Where(x=>x.EntryDate<=to); return q.OrderByDescending(x=>x.EntryDate).ThenByDescending(x=>x.Id).ToListAsync(cancellationToken); }
}
