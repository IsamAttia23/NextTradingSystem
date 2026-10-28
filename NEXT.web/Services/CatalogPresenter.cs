using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using NEXT.Application.DTOs.Catalog;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Presentation;
namespace NEXT.web.Services;
/// <summary>Builds reusable administration forms from application DTO metadata, never from domain entities.</summary>
public sealed class CatalogPresenter<TDto, TInput>(ICatalogService<TDto, TInput> service, IReferenceDataService references, QueryFilterPresenter filters)
    where TDto : class where TInput : class, new()
{
    public async Task<TableModel> TableAsync(string title, Func<string, object?, string> route, PaginationQuery query, CancellationToken ct)
    {
        var result = await service.ListAsync(query, ct);
        query.PageNumber = result.PageNumber; query.PageSize = result.PageSize;
        var columns = typeof(TInput).GetProperties().Where(p => p.Name is "Name" or "Code" or "Phone" or "Email" or "IsActive" or "Symbol" or "Color").Take(5).ToArray();
        var tagged = typeof(ITaggedRecordDto).IsAssignableFrom(typeof(TDto));
        var labels = columns.Select(x => x.Name == "IsActive" ? "Status" : x.Name).ToList();
        if (tagged) labels.Add("TagId");
        var filterFields = await filters.FieldsAsync(query, ct);
        return new(title, title + "Description", labels,
            result.Data.Select(item => new TableRow(columns.Select(p => Display(p.GetValue(item))).Concat(tagged ? [""] : Array.Empty<string>()).ToArray(),
                [new("Details", route("Details", new { id = Id(item), returnUrl = route("Index", query) }), Modal: false), new("Edit", route("Edit", new { id = Id(item) })), new("Delete", route("Delete", new { id = Id(item) }), true)],
                Badge(item), new RecordIdentityDto(Name(item), HasImage(item) ? route("Avatar", new { id = Id(item) }) : null))).ToArray(),
            new("Create", route("Create", null)), result.PageNumber, result.TotalPages, result.TotalCount,
            Query: query, SortOptions:
            typeof(TDto).GetProperties().Where(p => p.Name is "Name" or "Code" or "Email" or "Phone" or "CreditLimit" or "CurrentBalance" or "PaymentTermsDays" or "CostPrice" or "RetailPrice" or "WholesalePrice" or "VipPrice" or "TaxRate" or "MinStock" or "MaxStock")
                .Select(p => new FieldOption(p.Name, p.Name)).Append(new("CreatedAt", "CreatedAt")).ToArray(), FilterFields: filterFields, ActiveFilters: QueryFilterPresenter.ActiveCount(query), PageSize: result.PageSize);
    }
    public async Task<RecordDetailsDto> DetailsAsync(string section, Guid id, string? kind, int page, Func<string, object?, string> route, CancellationToken ct)
    {
        var item = await service.GetAsync(id, ct);
        var fields = new List<DetailFieldDto>();
        foreach (var p in typeof(TDto).GetProperties())
        {
            if (p.Name is "Id" or "Name" or "AvatarUrl" or "RemoveAvatar" or "TagName" or "TagColor") continue;
            var value = p.GetValue(item);
            if (p.Name == "TagId") continue;
            if (ReferenceKey(p.Name) is {} reference && value is Guid selected)
                fields.Add(new(p.Name, (await references.GetAsync(reference, ct)).SingleOrDefault(x => x.Value == selected.ToString())?.Label ?? "—"));
            else fields.Add(new(p.Name, value is decimal number ? number.ToString("N2") : Display(value),
                (Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType).IsEnum || p.PropertyType == typeof(bool)));
        }
        return new(section, Name(item), fields, new(Name(item), HasImage(item) ? route("Avatar", new { id }) : null), Badge(item),
            await service.ActivityAsync(id, kind, page, ct), route("Edit", new { id }), route("Index", null));
    }
    private static string Name(TDto item) => typeof(TDto).GetProperty("Name")?.GetValue(item)?.ToString() ?? typeof(TDto).Name;
    private static bool HasImage(TDto item) => item is IProfileImageInput { AvatarUrl.Length: > 0 };
    private static ColorBadgeDto? Badge(TDto item) => item is ITaggedRecordDto { TagName: {} name } tag ? new(name, tag.TagColor ?? "#778195") : null;
    public async Task<ModalForm> FormAsync(string title, string action, Guid? id, CancellationToken ct)
    {
        object input = id is {} key ? await service.GetAsync(key, ct) : new TInput();
        List<FormField> fields = [];
        foreach (var property in typeof(TInput).GetProperties())
        {
            if (property.Name == "AvatarUrl")
            {
                fields.Add(new("Avatar", "ProfileImage", id is not null && input is IProfileImageInput { AvatarUrl.Length: > 0 }
                    ? "data:image/png;base64," + Convert.ToBase64String(((IProfileImageInput)input).AvatarUrl!) : null, "file"));
                continue;
            }
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var value = property.GetValue(input);
            var reference = ReferenceKey(property.Name);
            IReadOnlyList<FieldOption>? options = null;
            var kind = "text";
            if (reference is not null)
            {
                kind = "select";
                options = (await references.GetAsync(reference, ct)).Where(x => !(property.Name is "ParentId" or "BaseUnitId" && x.Value == id?.ToString()))
                    .Select(x => new FieldOption(x.Value, x.Label)).ToArray();
            }
            else if (type.IsEnum)
            {
                kind = "select";
                options = Enum.GetValues(type).Cast<object>().Select(x => new FieldOption(x.ToString()!, x.ToString()!)).ToArray();
            }
            else if (type == typeof(bool)) kind = "checkbox";
            else if (type == typeof(decimal) || type == typeof(int)) kind = "number";
            else if (property.GetCustomAttribute<EmailAddressAttribute>() is not null) kind = "email";
            else if (property.Name is "Notes" or "Description" or "Address") kind = "textarea";
            else if (property.Name == "Color") kind = "color";
            fields.Add(new(property.Name, property.Name, Format(value), kind,
                property.GetCustomAttribute<RequiredAttribute>() is not null || (type == typeof(Guid) && Nullable.GetUnderlyingType(property.PropertyType) is null),
                property.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength, options));
        }
        return new(title, action, fields);
    }
    private static string? ReferenceKey(string name) => name switch
    {
        "TagId" => "Tags", "CustomerId" => "Customers", "ParentId" or "CategoryId" => "Categories",
        "BranchId" => "Branches", "BaseUnitId" or "UnitOfMeasureId" => "Units", _ => null
    };
    private static object? Id(TDto item) => typeof(TDto).GetProperty("Id")!.GetValue(item);
    private static string Display(object? value) => value is bool flag ? flag ? "Active" : "Inactive" : string.IsNullOrWhiteSpace(Format(value)) ? "—" : Format(value)!;
    private static string? Format(object? value) => value is bool flag ? flag.ToString().ToLowerInvariant() : Convert.ToString(value, CultureInfo.InvariantCulture);
}
