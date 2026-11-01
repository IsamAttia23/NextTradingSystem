using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

public sealed class JournalEntryRepository(NEXTDbContext context) : KeyRepository<JournalEntry, Guid>(context), IJournalEntryRepository
{
    public Task<JournalEntry?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => Context.JournalEntries.AsNoTracking()
        .Include(x => x.Lines)
        .ThenInclude(x => x.Account)
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public override Expression<Func<JournalEntry, bool>> BuildFilterExpression(PaginationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return BuildFilterExpression(query as JournalEntryQueryModel ?? new JournalEntryQueryModel { SearchTerm = query.SearchTerm });
    }

    public Expression<Func<JournalEntry, bool>> BuildFilterExpression(JournalEntryQueryModel query)
    {
        ArgumentNullException.ThrowIfNull(query);
        Expression<Func<JournalEntry, bool>> filters = entity =>
            (!query.From.HasValue || entity.EntryDate >= query.From.Value) &&
            (!query.To.HasValue || entity.EntryDate <= query.To.Value);

        return CombineSearchExpression(filters, query.SearchTerm, "EntryNumber", "Description");
    }

    public override IQueryable<JournalEntry> ApplySorting(IQueryable<JournalEntry> entities, string? sortBy, SortDirection? sortDirection)
        => base.ApplySorting(entities, string.IsNullOrWhiteSpace(sortBy) ? nameof(JournalEntry.EntryDate) : sortBy,
            sortDirection ?? SortDirection.Descending);
}
