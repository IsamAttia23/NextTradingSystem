using System.Linq.Expressions;
using System.Reflection;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.Exceptions;
using NEXT.Domain.Enums;
namespace NEXT.Application.Services.Catalog;
public static class CatalogQueryPipeline
{
    private static readonly HashSet<string> CommonFields = typeof(PaginationQuery).GetProperties().Select(p => p.Name).ToHashSet();
    private static readonly string[] SearchFields = ["Name", "Code", "Email", "Phone", "Barcode", "Symbol", "CompanyName", "Description", "BatchNumber", "Product.Name", "Warehouse.Name", "Customer.Name", "Supplier.Name"];
    public static IQueryable<T> Apply<T>(IQueryable<T> source, PaginationQuery query, string? dateMember = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateRanges(query);
        var parameter = Expression.Parameter(typeof(T), "entity");
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = Text(query.SearchTerm, "SearchTerm");
            Expression? combined = null;
            foreach (var name in SearchFields)
            {
                Expression property = parameter; Expression? guard = null; var valid = true;
                foreach (var part in name.Split('.'))
                {
                    var member = property.Type.GetProperty(part);
                    if (member is null) { valid = false; break; }
                    if (property != parameter && !property.Type.IsValueType)
                    {
                        var notNull = Expression.NotEqual(property, Expression.Constant(null, property.Type));
                        guard = guard is null ? notNull : Expression.AndAlso(guard, notNull);
                    }
                    property = Expression.Property(property, member);
                }
                if (!valid || property.Type != typeof(string)) continue;
                Expression contains = Contains(property, term);
                if (guard is not null) contains = Expression.AndAlso(guard, contains);
                combined = combined is null ? contains : Expression.OrElse(combined, contains);
            }
            if (combined is not null) source = source.Where(Expression.Lambda<Func<T,bool>>(combined, parameter));
        }
        foreach (var field in query.GetType().GetProperties().Where(p => !CommonFields.Contains(p.Name)))
        {
            var value = field.GetValue(query);
            if (value is null || value is string text && string.IsNullOrWhiteSpace(text)) continue;
            var type = Nullable.GetUnderlyingType(field.PropertyType) ?? field.PropertyType;
            if (type.IsEnum && !Enum.IsDefined(type, value)) Fail(field.Name, "InvalidFilterValue");
            ExpressionType operation = ExpressionType.Equal;
            string member = field.Name;
            var date = member is "From" or "To";
            if (date)
            {
                member = dateMember ?? "CreatedAt";
                operation = field.Name == "From" ? ExpressionType.GreaterThanOrEqual : ExpressionType.LessThan;
            }
            else if (field.Name.StartsWith("Min", StringComparison.Ordinal) || field.Name.StartsWith("Max", StringComparison.Ordinal))
            {
                operation = field.Name.StartsWith("Min", StringComparison.Ordinal) ? ExpressionType.GreaterThanOrEqual : ExpressionType.LessThanOrEqual;
                member = typeof(T).GetProperty(field.Name[3..]) is not null ? field.Name[3..] : field.Name;
            }
            Expression? property = typeof(T).GetProperty(member) is not null ? Expression.Property(parameter, member) : null;
            // AvailableQuantity is intentionally not persisted; filter its arithmetic expression in SQL.
            if (member == "AvailableQuantity" && typeof(T).GetProperty("ReservedQuantity") is not null)
                property = Expression.Subtract(Expression.Property(parameter, "Quantity"), Expression.Property(parameter, "ReservedQuantity"));
            if (property is null) continue;
            Expression predicate;
            if (value is string stringValue)
                predicate = Contains(property, Text(stringValue, field.Name));
            else
            {
                var targetType = Nullable.GetUnderlyingType(property.Type) ?? property.Type;
                if (date)
                {
                    var day = value is DateOnly only ? only : DateOnly.FromDateTime(((DateTimeOffset)value).Date);
                    if (targetType == typeof(DateOnly))
                    {
                        value = day;
                        if (field.Name == "To") operation = ExpressionType.LessThanOrEqual;
                    }
                    else
                    {
                        if (field.Name == "To" && day == DateOnly.MaxValue) continue;
                        value = new DateTimeOffset((field.Name == "To" ? day.AddDays(1) : day).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                    }
                }
                var constant = Expression.Convert(Expression.Constant(value), property.Type);
                predicate = Expression.MakeBinary(operation, property, constant);
            }
            source = source.Where(Expression.Lambda<Func<T,bool>>(predicate, parameter));
        }
        var sort = typeof(T).GetProperties().FirstOrDefault(p => p.Name.Equals(query.SortBy ?? "Name", StringComparison.OrdinalIgnoreCase));
        var sortType = sort is null ? null : Nullable.GetUnderlyingType(sort.PropertyType) ?? sort.PropertyType;
        if (sort is null || !(sortType == typeof(string) || sortType == typeof(bool) || sortType == typeof(decimal) || sortType == typeof(int) || sortType == typeof(DateTimeOffset) || sortType == typeof(DateOnly)))
            sort = typeof(T).GetProperty("Id")!;
        var selector = Expression.Lambda(Expression.Property(parameter, sort), parameter);
        var sorted = source.Provider.CreateQuery<T>(Expression.Call(typeof(Queryable), query.SortDirection == SortDirection.Descending ? "OrderByDescending" : "OrderBy", [typeof(T), sort.PropertyType], source.Expression, Expression.Quote(selector)));
        var id = Expression.Lambda(Expression.Property(parameter, "Id"), parameter);
        return sorted.Provider.CreateQuery<T>(Expression.Call(typeof(Queryable), "ThenBy", [typeof(T), typeof(Guid)], sorted.Expression, Expression.Quote(id)));
    }
    private static Expression Contains(Expression property, string term) => Expression.AndAlso(Expression.NotEqual(property, Expression.Constant(null, typeof(string))),
        Expression.Call(Expression.Call(property, nameof(string.ToLower), Type.EmptyTypes), nameof(string.Contains), Type.EmptyTypes, Expression.Constant(term)));
    private static string Text(string value, string field)
    {
        if (value.Length > 200) Fail(field, "FilterTextTooLong");
        return value.Trim().ToLowerInvariant();
    }
    private static void ValidateRanges(PaginationQuery query)
    {
        foreach (var min in query.GetType().GetProperties())
        {
            var upperName = min.Name == "From" ? "To" : min.Name.StartsWith("Min", StringComparison.Ordinal) ? "Max" + min.Name[3..] : null;
            if (upperName is null) continue;
            var lower = min.GetValue(query); var upper = query.GetType().GetProperty(upperName)?.GetValue(query);
            if (lower is IComparable comparable && upper is not null && comparable.CompareTo(upper) > 0) Fail(upperName, "FilterRangeInvalid");
        }
    }
    private static void Fail(string field, string message) => throw new AppValidationException(new Dictionary<string,string[]> { [field] = [message] });
}
