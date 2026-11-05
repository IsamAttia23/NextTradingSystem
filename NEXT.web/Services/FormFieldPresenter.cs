using NEXT.web.Models;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Common;
using NEXT.Application.DTOs.Presentation;

namespace NEXT.web.Services;

/// <summary>Shared field descriptions and formatting for forms, filters and details.</summary>
public sealed class FormFieldPresenter(IReferenceDataService references)
{
    private readonly Dictionary<string, Task<IReadOnlyList<FieldOption>>> _options = [];

    public Task<IReadOnlyList<FieldOption>> OptionsAsync(string key, CancellationToken ct)
    {
        if (!_options.TryGetValue(key, out var options))
        {
            _options[key] = options = LoadOptionsAsync(key, ct);
        }
        return options;
    }

    private async Task<IReadOnlyList<FieldOption>> LoadOptionsAsync(string key, CancellationToken ct) =>
        (await references.GetAsync(key, ct)).Select(x => new FieldOption(x.Value, x.Label)).ToArray();

    public async Task<IReadOnlyList<FormField>> FormAsync<TInput>(TInput input, CancellationToken ct,
        Guid? excludedId = null, params string[] includedFields) where TInput : class
    {
        List<FormField> fields = [];
        foreach (var property in typeof(TInput).GetProperties())
        {
            if (property.Name == "Id"
                || includedFields.Length > 0
                && !includedFields.Contains(property.Name))
            {
                continue;
            }
            if (property.Name == "AvatarUrl")
            {
                fields.Add(new("Avatar", "ProfileImage", Avatar(input as IProfileImageInput), "file"));
            }
            else
            {
                fields.Add(await FieldAsync(property, property.GetValue(input), false, ct, excludedId));
            }
        }
        return fields;
    }

    public async Task<FormField> FieldAsync(PropertyInfo property, object? value, bool filter,
        CancellationToken ct, Guid? excludedId = null, string? label = null)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var kind = "text";
        IReadOnlyList<FieldOption>? options = null;
        if (ReferenceKey(property.Name) is { } key)
        {
            kind = "select";
            options = await OptionsAsync(key, ct);
            if (!filter
                && excludedId.HasValue
                && property.Name is "ParentId" or "BaseUnitId")
            {
                options = options.Where(x => x.Value != excludedId.Value.ToString()).ToArray();
            }
        }
        else if (type.IsEnum)
        {
            kind = "select";
            options = Enum.GetValues(type).Cast<object>().Select(x => new FieldOption(x.ToString()!, x.ToString()!)).ToArray();
        }
        else if (type == typeof(bool))
        {
            kind = filter ? "select" : "checkbox";
            if (filter)
            {
                options = [new("true", property.Name == "IsActive" ? "Active" : "Yes"), new("false", property.Name == "IsActive" ? "Inactive" : "No")];
            }
        }
        else if (type == typeof(decimal) || type == typeof(int))
        {
            kind = "number";
        }
        else if (type == typeof(DateOnly) || type == typeof(DateTimeOffset))
        {
            kind = "date";
        }
        else if (!filter && property.GetCustomAttribute<EmailAddressAttribute>() is not null)
        {
            kind = "email";
        }
        else if (!filter && property.Name is "Notes" or "Description" or "Address")
        {
            kind = "textarea";
        }
        else if (!filter && property.Name == "Color")
        {
            kind = "color";
        }
        return new(property.Name, label ?? property.Name, Format(value), kind,
            !filter && (property.GetCustomAttribute<RequiredAttribute>() is not null || property.PropertyType == typeof(Guid)),
            property.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength ?? (filter && type == typeof(string) ? 200 : null), options);
    }

    public async Task<IReadOnlyList<DetailFieldDto>> DetailsAsync<TDto>(TDto item, CancellationToken ct) where TDto : class
    {
        List<DetailFieldDto> fields = [];
        foreach (var property in typeof(TDto).GetProperties())
        {
            if (property.Name is "Id" or "Name" or "AvatarUrl" or "RemoveAvatar" or "TagName" or "TagColor" or "TagId" or "RowVersion")
            {
                continue;
            }
            var value = property.GetValue(item);
            if (ReferenceKey(property.Name) is { } key && value is Guid selected)
            {
                fields.Add(new(property.Name, (await OptionsAsync(key, ct)).SingleOrDefault(x => x.Value == selected.ToString())?.Label ?? "—"));
            }
            else
            {
                fields.Add(new(property.Name, value is decimal number ? DisplayNumber(property.Name, number) : Display(value),
                                (Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType).IsEnum || property.PropertyType == typeof(bool)));
            }
        }
        return fields;
    }

    public static string? Avatar(IProfileImageInput? input) => Avatar(input?.AvatarUrl);

    public static string? Avatar(byte[]? data) => data is { Length: > 0 } bytes
        ? "data:image/png;base64," + Convert.ToBase64String(bytes) : null;

    public static string Quantity(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);

    public static string DisplayNumber(string name, decimal value) =>
        name is "Quantity" or "ReservedQuantity" or "AvailableQuantity" or "MinStock" or "MaxStock"
            ? Quantity(value) : value.ToString("N2", CultureInfo.CurrentCulture);

    public static string Display(object? value)
    {
        if (value is bool flag)
        {
            return flag ? "Active" : "Inactive";
        }
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(text) ? "—" : text;
    }

    public static string? Format(object? value) => value switch
    {
        bool flag => flag.ToString().ToLowerInvariant(),
        decimal number => Quantity(number),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };

    private static string? ReferenceKey(string name) => name switch
    {
        "TagId" => "Tags",
        "CustomerId" => "Customers",
        "ParentId" or "CategoryId" => "Categories",
        "BranchId" => "Branches",
        "BaseUnitId" or "UnitOfMeasureId" => "Units",
        "ProductId" => "Products",
        "WarehouseId" => "Warehouses",
        _ => null
    };
}
