using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
namespace NEXT.Infrastructure.Persistence;
/// <summary>Read-only related-record graph, including invoice/payment ledger references without physical foreign keys.</summary>
public sealed class RelatedRecordsReader(NEXTDbContext db) : IRelatedRecordsReader
{
    private static readonly HashSet<string> DisplayFields = ["Name", "FullName", "UserName", "Code", "Number", "InvoiceNumber", "PaymentNumber", "EntryNumber", "BatchNumber",
        "InvoiceDate", "OrderDate", "PaymentDate", "EntryDate", "ExpiryDate", "CreatedAt", "Type", "InvoiceType", "PaymentType", "Status", "PaymentStatus",
        "Quantity", "ReservedQuantity", "AverageCost", "UnitPrice", "UnitCost", "CostPrice", "TotalAmount", "Amount", "PaidAmount", "RemainingAmount",
        "TotalDebit", "TotalCredit", "Debit", "Credit", "Phone", "Email", "Position", "Description", "Notes", "IsActive"];
    public async Task<ActivityPageDto> ReadAsync<TEntity>(Guid id, string? kind, int page, CancellationToken ct) where TEntity : class
    {
        var root = Set(typeof(TEntity));
        var p = Expression.Parameter(typeof(TEntity), "root");
        root = Where(root, Expression.Lambda(Expression.Equal(Expression.Property(p, "Id"), Expression.Constant(id)), p));
        var found = new Dictionary<Type, IQueryable>();
        root = await ExpandSelf(root, ct);
        if (db.Model.FindEntityType(typeof(TEntity))!.GetForeignKeys().Any(x => x.PrincipalEntityType.ClrType == typeof(TEntity)))
        {
            var descendant = Expression.Parameter(typeof(TEntity), "descendant");
            found[typeof(TEntity)] = Where(root, Expression.Lambda(Expression.NotEqual(Expression.Property(descendant, "Id"), Expression.Constant(id)), descendant));
        }
        await Explore(typeof(TEntity), root, [typeof(TEntity)], found, ct);
        var sections = new List<ActivitySectionDto>();
        foreach (var entry in found.OrderBy(x => x.Key.Name))
        {
            ct.ThrowIfCancellationRequested();
            var count = await Count(entry.Value, ct);
            // Empty sections are kept so users can distinguish no records from an unsupported relation.
            sections.Add(new(entry.Key.Name, entry.Key.Name, count));
        }
        if (kind is not null && !sections.Any(x => x.Key == kind)) throw new AppValidationException(new Dictionary<string,string[]> { ["activityKind"] = ["Unknown related record section."] });
        kind ??= sections.FirstOrDefault(x => x.Count > 0)?.Key ?? sections.FirstOrDefault()?.Key;
        if (kind is null) return new(sections, null, [], 1, 0, 0);
        var selected = sections.Single(x => x.Key == kind);
        var totalPages = (int)Math.Ceiling(selected.Count / 10d);
        page = Math.Clamp(page, 1, Math.Max(1, totalPages));
        var query = found.Single(x => x.Key.Name == kind).Value;
        var records = await Fetch(query, page, ct);
        return new(sections, kind, records, page, totalPages, selected.Count);
    }
    private async Task Explore(Type parentType, IQueryable parent, HashSet<Type> path, Dictionary<Type, IQueryable> found, CancellationToken ct)
    {
        var parentMetadata = db.Model.FindEntityType(parentType)!;
        foreach (var fk in parentMetadata.GetReferencingForeignKeys())
        {
            var childType = fk.DeclaringEntityType.ClrType;
            if (path.Contains(childType) || fk.Properties.Count != 1 || fk.PrincipalKey.Properties.Count != 1 ||
                childType.Namespace != "NEXT.Domain.Entities") continue;
            // Security rows contain secrets and are deliberately excluded from catalog details.
            if (childType == typeof(ApplicationUser) || childType == typeof(UserRole) || childType == typeof(RolePermission)) continue;
            await AddChild(childType, Child(parent, childType, fk.Properties[0].Name, fk.PrincipalKey.Properties[0].Name), path, found, ct);
        }
        if (parentType == typeof(SalesInvoice) || parentType == typeof(PurchaseInvoice) || parentType == typeof(Payment))
        {
            var reference = parentType == typeof(SalesInvoice) ? JournalReferenceType.SalesInvoice : parentType == typeof(PurchaseInvoice) ? JournalReferenceType.PurchaseInvoice : JournalReferenceType.Payment;
            if (!path.Contains(typeof(JournalEntry))) await AddChild(typeof(JournalEntry), Child(parent, typeof(JournalEntry), "ReferenceId", "Id", "ReferenceType", reference), path, found, ct);
            if (!path.Contains(typeof(StockMovement))) await AddChild(typeof(StockMovement), Child(parent, typeof(StockMovement), "ReferenceId", "Id", "ReferenceType", parentType.Name), path, found, ct);
        }
    }
    private async Task AddChild(Type type, IQueryable query, HashSet<Type> path, Dictionary<Type, IQueryable> found, CancellationToken ct)
    {
        query = await ExpandSelf(query, ct);
        found[type] = found.TryGetValue(type, out var existing)
            ? query.Provider.CreateQuery(Expression.Call(typeof(Queryable), nameof(Queryable.Union), [type], existing.Expression, query.Expression)) : query;
        await Explore(type, query, new HashSet<Type>(path) { type }, found, ct);
    }
    private async Task<IQueryable> ExpandSelf(IQueryable query, CancellationToken ct)
    {
        var type = query.ElementType;
        var keys = db.Model.FindEntityType(type)!.GetForeignKeys().Where(x => x.PrincipalEntityType.ClrType == type && x.Properties.Count == 1).ToArray();
        if (keys.Length == 0) return query;
        var visited = (await Ids(query, ct)).ToHashSet();
        var frontier = visited.ToArray();
        while (frontier.Length > 0)
        {
            var newIds = new HashSet<Guid>();
            foreach (var fk in keys)
            {
                var parameter = Expression.Parameter(type, "child");
                Expression property = Expression.Property(parameter, fk.Properties[0].Name);
                Expression predicate;
                if (property.Type == typeof(Guid?))
                    predicate = Expression.AndAlso(Expression.Property(property, "HasValue"), Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(Guid)], Expression.Constant(frontier), Expression.Property(property, "Value")));
                else predicate = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(Guid)], Expression.Constant(frontier), property);
                foreach (var id in await Ids(Where(Set(type), Expression.Lambda(predicate, parameter)), ct))
                    if (visited.Add(id)) newIds.Add(id);
            }
            frontier = newIds.ToArray();
        }
        var record = Expression.Parameter(type, "record");
        return Where(Set(type), Expression.Lambda(Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(Guid)], Expression.Constant(visited.ToArray()), Expression.Property(record, "Id")), record));
    }
    private static Task<List<Guid>> Ids(IQueryable query, CancellationToken ct) =>
        (Task<List<Guid>>)typeof(RelatedRecordsReader).GetMethod(nameof(IdsOf), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(query.ElementType).Invoke(null, [query, ct])!;
    private static Task<List<Guid>> IdsOf<T>(IQueryable<T> query, CancellationToken ct)
    {
        var parameter = Expression.Parameter(typeof(T), "record");
        return query.Select(Expression.Lambda<Func<T,Guid>>(Expression.Property(parameter, "Id"), parameter)).ToListAsync(ct);
    }
    private IQueryable Child(IQueryable parent, Type childType, string foreignKey, string principalKey, string? discriminator = null, object? expected = null)
    {
        var child = Expression.Parameter(childType, "child");
        var principal = Expression.Parameter(parent.ElementType, "principal");
        var left = Expression.Property(principal, principalKey);
        var right = Expression.Property(child, foreignKey);
        Expression equal = left.Type == right.Type ? Expression.Equal(left, right) : Expression.Equal(Expression.Convert(left, right.Type), right);
        Expression predicate = Expression.Call(typeof(Queryable), nameof(Queryable.Any), [parent.ElementType], parent.Expression, Expression.Quote(Expression.Lambda(equal, principal)));
        if (discriminator is not null)
        {
            var property = Expression.Property(child, discriminator);
            predicate = Expression.AndAlso(predicate, Expression.Equal(property, Expression.Convert(Expression.Constant(expected), property.Type)));
        }
        return Where(Set(childType), Expression.Lambda(predicate, child));
    }
    private IQueryable Set(Type type) => (IQueryable)typeof(RelatedRecordsReader).GetMethod(nameof(SetOf), BindingFlags.NonPublic | BindingFlags.Instance)!.MakeGenericMethod(type).Invoke(this, null)!;
    private IQueryable<T> SetOf<T>() where T : class => db.Set<T>().AsNoTracking();
    private static IQueryable Where(IQueryable query, LambdaExpression predicate) => query.Provider.CreateQuery(Expression.Call(typeof(Queryable), nameof(Queryable.Where), [query.ElementType], query.Expression, Expression.Quote(predicate)));
    private static Task<int> Count(IQueryable query, CancellationToken ct) =>
        (Task<int>)typeof(RelatedRecordsReader).GetMethod(nameof(CountOf), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(query.ElementType).Invoke(null, [query, ct])!;
    private static Task<int> CountOf<T>(IQueryable<T> query, CancellationToken ct) => query.CountAsync(ct);
    private static Task<IReadOnlyList<RelatedRecordDto>> Fetch(IQueryable query, int page, CancellationToken ct) =>
        (Task<IReadOnlyList<RelatedRecordDto>>)typeof(RelatedRecordsReader).GetMethod(nameof(FetchOf), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(query.ElementType).Invoke(null, [query, page, ct])!;
    private static async Task<IReadOnlyList<RelatedRecordDto>> FetchOf<T>(IQueryable<T> query, int page, CancellationToken ct)
    {
        var parameter = Expression.Parameter(typeof(T), "record");
        var selector = Expression.Lambda<Func<T, Guid>>(Expression.Property(parameter, "Id"), parameter);
        var records = await query.OrderBy(selector).Skip((page - 1) * 10).Take(10).ToListAsync(ct);
        var properties = typeof(T).GetProperties().Where(x => DisplayFields.Contains(x.Name)).ToArray();
        return records.Select(x => new RelatedRecordDto((Guid)typeof(T).GetProperty("Id")!.GetValue(x)!, properties
            .Select(p => new DetailFieldDto(p.Name, p.GetValue(x) is decimal quantity && p.Name.Contains("Quantity") ? quantity.ToString("N3") : Format(p.GetValue(x)), (Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType).IsEnum || p.PropertyType == typeof(bool))).ToArray())).ToArray();
    }
    private static string Format(object? value) => value switch
    {
        null => "—", DateTimeOffset date => date.ToLocalTime().ToString("g"), DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal number => number.ToString("N2"), bool flag => flag ? "Active" : "Inactive", _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? "—"
    };
}
