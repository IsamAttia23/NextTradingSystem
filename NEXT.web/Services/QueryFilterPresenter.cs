using NEXT.web.Models;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.QueryModels;
using NEXT.Application.DTOs.Presentation;
using NEXT.Domain.Enums;

namespace NEXT.web.Services;

public sealed class QueryFilterPresenter(FormFieldPresenter fields)
{
    private static readonly HashSet<string> Common = typeof(PaginationQuery).GetProperties().Select(p => p.Name).ToHashSet();
    public async Task<IReadOnlyList<FormField>> FieldsAsync(PaginationQuery query, CancellationToken ct)
    {
        var result = new List<FormField> { new("SearchTerm", "SearchAll", query.SearchTerm, "search", MaxLength: 200) };
        foreach (var property in query.GetType().GetProperties().Where(p => !Common.Contains(p.Name)))
        {
            if (query is AccountingQuery && property.Name is "CustomerId" or "InvoiceId" or "PaymentId") continue;
            if (query is ProductQueryModel product)
            {
                if (property.Name == nameof(ProductQueryModel.IsAvailable))
                {
                    continue;
                }
                if (property.Name == nameof(ProductQueryModel.Availability))
                {
                    var availability = product.Availability ??
                        (product.IsAvailable == false ? ProductAvailability.Unavailable : (ProductAvailability?)null);
                    result.Add(new(property.Name, "Status", availability?.ToString(), "select",
                        Options: [new(nameof(ProductAvailability.Available), "ProductAvailable"),
                            new(nameof(ProductAvailability.LowStock), "ProductLowStock"),
                            new(nameof(ProductAvailability.Unavailable), "ProductUnavailable")]));
                    continue;
                }
            }

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
        .Count(p => (!Common.Contains(p.Name) || p.Name == "SearchTerm")
            && (p.Name != nameof(ProductQueryModel.IsAvailable) || query is not ProductQueryModel { Availability: not null })
            && p.GetValue(query) is { } value && (value is not string text || !string.IsNullOrWhiteSpace(text)));
}
