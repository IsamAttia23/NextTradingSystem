using System.Globalization;
using NEXT.Application.DTOs.Presentation;
namespace NEXT.web.Services;
public static class RelatedRecordPresenter
{
    public static IReadOnlyList<DetailFieldDto> Fields(RelatedRecordDto record) => record.Values
        .Select(field => new DetailFieldDto(field.Key, Format(field.Key, field.Value), field.Value is Enum or bool)).ToArray();
    private static string Format(string name, object? value) => value switch
    {
        null => "—",
        DateTimeOffset date => date.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal number => number.ToString(name.Contains("Quantity") ? "N3" : "N2", CultureInfo.CurrentCulture),
        bool flag => flag ? "Active" : "Inactive",
        _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? "—"
    };
}
