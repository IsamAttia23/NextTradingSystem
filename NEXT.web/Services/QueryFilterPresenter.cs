using NEXT.web.Models;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.QueryModels;
using NEXT.Application.DTOs.Presentation;

namespace NEXT.web.Services;

public sealed class QueryFilterPresenter(FormFieldPresenter fields)
{
    private static readonly HashSet<string> Common = typeof(PaginationQuery).GetProperties().Select(p => p.Name).ToHashSet();
    public async Task<IReadOnlyList<FormField>> FieldsAsync(PaginationQuery query, CancellationToken ct)
    {
        var result = new List<FormField> { new("SearchTerm", "SearchAll", query.SearchTerm, "search", MaxLength: 200) };
        foreach (var property in query.GetType().GetProperties().Where(p => !Common.Contains(p.Name)))
        {
            var label = (query, property.Name) switch
            {
                (CustomerQueryModel, "Type") => "CustomerTypeFilter",
                (CustomerQueryModel, "Category") => "CustomerCategoryFilter",
                (StockMovementQueryModel, "Type") => "MovementType",
                _ => property.Name
            };
            result.Add(await fields.FieldAsync(property, property.GetValue(query), true, ct, label: label));
        }
        return result;
    }
    public static int ActiveCount(PaginationQuery query) => query.GetType().GetProperties()
        .Count(p => (!Common.Contains(p.Name) || p.Name == "SearchTerm") && p.GetValue(query) is { } value && (value is not string text || !string.IsNullOrWhiteSpace(text)));
}
