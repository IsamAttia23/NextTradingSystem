using NEXT.Application.QueryModels;
using System.Globalization;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Presentation;
namespace NEXT.web.Services;
/// <summary>One form renderer for optional fields declared by an entity's QueryModel.</summary>
public sealed class QueryFilterPresenter(IReferenceDataService references)
{
    private static readonly HashSet<string> Common = typeof(PaginationQuery).GetProperties().Select(p => p.Name).ToHashSet();
    public async Task<IReadOnlyList<FormField>> FieldsAsync(PaginationQuery query, CancellationToken ct)
    {
        var fields = new List<FormField> { new("SearchTerm", "SearchAll", query.SearchTerm, "search", MaxLength: 200) };
        foreach (var property in query.GetType().GetProperties().Where(p => !Common.Contains(p.Name)))
        {
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var value = property.GetValue(query);
            string kind = "text";
            IReadOnlyList<FieldOption>? options = null;
            var reference = ReferenceKey(property.Name);
            if (reference is not null)
            {
                kind = "select";
                options = (await references.GetAsync(reference, ct)).Select(x => new FieldOption(x.Value, x.Label)).ToArray();
            }
            else if (type.IsEnum)
            {
                kind = "select"; options = Enum.GetValues(type).Cast<object>().Select(x => new FieldOption(x.ToString()!, x.ToString()!)).ToArray();
            }
            else if (type == typeof(bool))
            {
                kind = "select"; options = [new("true", property.Name == "IsActive" ? "Active" : "Yes"), new("false", property.Name == "IsActive" ? "Inactive" : "No")];
            }
            else if (type == typeof(decimal) || type == typeof(int)) kind = "number";
            else if (type == typeof(DateOnly) || type == typeof(DateTimeOffset)) kind = "date";
            var label = (query, property.Name) switch
            {
                (CustomerQueryModel, "Type") => "CustomerTypeFilter", (CustomerQueryModel, "Category") => "CustomerCategoryFilter",
                (StockMovementQueryModel, "Type") => "MovementType", (ProductBatchQueryModel, "From") => "ExpiryFrom", (ProductBatchQueryModel, "To") => "ExpiryThrough",
                _ => property.Name
            };
            fields.Add(new(property.Name, label, Format(value), kind, MaxLength: type == typeof(string) ? 200 : null, Options: options));
        }
        return fields;
    }
    public static int ActiveCount(PaginationQuery query) => query.GetType().GetProperties()
        .Count(p => (!Common.Contains(p.Name) || p.Name == "SearchTerm") && p.GetValue(query) is {} value && (value is not string text || !string.IsNullOrWhiteSpace(text)));
    private static string? Format(object? value) => value switch
    {
        bool flag => flag.ToString().ToLowerInvariant(), DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };
    private static string? ReferenceKey(string name) => name switch
    {
        "TagId" => "Tags", "CustomerId" => "Customers", "ParentId" or "CategoryId" => "Categories", "BranchId" => "Branches",
        "BaseUnitId" or "UnitOfMeasureId" => "Units", "ProductId" => "Products", "WarehouseId" => "Warehouses", _ => null
    };
}
