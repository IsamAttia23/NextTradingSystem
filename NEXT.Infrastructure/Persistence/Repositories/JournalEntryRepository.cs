using NEXT.Application.Common.QueryModels;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Application.QueryModels;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for JournalEntry; inherits common CRUD behavior from the generic repository.</summary>
public sealed class JournalEntryRepository(NEXTDbContext context) : KeyRepository<JournalEntry, Guid>(context), IJournalEntryRepository
{
    public override IQueryable<JournalEntry> Search(PaginationQuery query) =>
        Search(query as JournalEntryQueryModel ?? new JournalEntryQueryModel { SearchTerm = query.SearchTerm, SortBy = query.SortBy,
            SortDirection = query.SortDirection, PageNumber = query.PageNumber, PageSize = query.PageSize });

    public Task<JournalEntry?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => Context.JournalEntries.AsNoTracking()
        .Include(x => x.Lines)
        .ThenInclude(x => x.Account)
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public IQueryable<JournalEntry> Search(JournalEntryQueryModel queryModel, CancellationToken cancellationToken = default)
    {
        var enteries = Context.JournalEntries.AsNoTracking().AsQueryable();
        if (queryModel.From.HasValue) enteries = enteries.Where(x => x.EntryDate >= queryModel.From);
        if (queryModel.To.HasValue) enteries = enteries.Where(x => x.EntryDate <= queryModel.To);

        enteries = queryModel.SortDirection switch
        {
            SortDirection.Ascending => enteries
                .OrderBy(x => x.EntryDate)
                .ThenBy(x => x.Id),

            SortDirection.Descending => enteries
                .OrderByDescending(x => x.EntryDate)
                .ThenByDescending(x => x.Id),

            _ => enteries
                .OrderByDescending(x => x.EntryDate)
                .ThenByDescending(x => x.Id)
        };

        return enteries;
    }
}
