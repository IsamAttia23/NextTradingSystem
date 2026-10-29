using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;

namespace NEXT.Application.Common.QueryModels;

public class PaginationQuery : IValidatableObject
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; }
    public SortDirection? SortDirection { get; set; }
    [StringLength(200)]
    public string? SearchTerm { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var property in GetType().GetProperties())
        {
            var value = property.GetValue(this);
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (value is not null && type.IsEnum && !Enum.IsDefined(type, value))
                yield return new("Select a valid value.", [property.Name]);
            if (value is not IComparable minimum || !property.Name.StartsWith("Min")) continue;
            var maximum = GetType().GetProperty("Max" + property.Name[3..])?.GetValue(this);
            if (maximum is not null && minimum.CompareTo(maximum) > 0)
                yield return new("Maximum must be at least the minimum.", ["Max" + property.Name[3..]]);
        }
        if (GetType().GetProperty("From")?.GetValue(this) is IComparable from &&
            GetType().GetProperty("To")?.GetValue(this) is { } to && from.CompareTo(to) > 0)
            yield return new("End date must be on or after the start date.", ["To"]);
    }
}
