using NEXT.web.Models;
using System.Reflection;
using MediatR;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Common;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.DTOs.Tags;
using NEXT.Application.DTOs.Customers;
using NEXT.Application.DTOs.CustomerContacts;

namespace NEXT.web.Services;

public sealed class EntityPresenter<TDto, TInput>(ISender sender, TablePresenter tables, FormFieldPresenter fields, NEXT.Application.Services.Currencies.ExchangeRateReader? exchangeRates = null)
    where TDto : class, TInput, IRecordDto where TInput : class, new()
{
    public async Task<TableModel> TableAsync(string title, Func<string, object?, string> route, PaginationQuery query, CancellationToken ct)
    {
        var pagedList = await sender.Send(new GetEntitiesQuery<TDto>(query), ct);
        var columns = typeof(TInput).GetProperties()
            .Where(p => p.Name is "Name" or "Code" or "Phone" or "Email" or "IsActive" or "Symbol" or "Color")
            .Where(p => typeof(TDto) != typeof(TagDto) || p.Name != "Color").Take(5).ToArray();
        var tagged = typeof(ITaggedRecordDto).IsAssignableFrom(typeof(TDto));
        var labels = columns.Select(x => x.Name == "IsActive" ? "Status" : x.Name).ToList();
        if (tagged)
        {
            labels.Add("TagId");
        }
        return await tables.BuildAsync(title, labels, pagedList, query,
                    item => Row(item, columns, tagged, route, query),
                    SortFields(query).Select(name => new FieldOption(name, name)).ToArray(), ct, new("Create", route("Create", null)));
    }

    private static TableRow Row(TDto item, PropertyInfo[] columns, bool tagged,
        Func<string, object?, string> route, PaginationQuery query) =>
        new(columns.Select(p => FormFieldPresenter.Display(p.GetValue(item))).Concat(tagged ? [""] : Array.Empty<string>()).ToArray(),
            [new("Details", route("Details", new { id = item.Id, returnUrl = route("Index", query) }), Modal: false),
             new("Edit", route("Edit", new { id = item.Id })), new("Delete", route("Delete", new { id = item.Id }), true)],
            Badge(item), Identity(item, route), BadgeColumn: item is TagDto ? "Name" : "TagId");

    public async Task<RecordDetailsDto> DetailsAsync(string section, Guid id, string? kind, int page,
        Func<string, object?, string> route, CancellationToken ct)
    {
        var item = await FindAsync(id, ct);
        var details = await fields.DetailsAsync(item, ct);
        var showActivity = item is not CustomerContactDto;
        var activity = showActivity
            ? await sender.Send(new GetEntityActivityQuery<TDto>(id, kind, page), ct)
            : new ActivityPageDto([], null, [], 1, 0, 0);
        return new(section, Name(item), details, Identity(item, route), Badge(item), activity,
            route("Edit", new
            {
                id
            }), route("Index", null),
            item is CustomerDto ? new("AddContact", route("Create", new { controller = "CustomersContact", customerId = id })) : null,
            showActivity);
    }

    public async Task<ModalForm> FormAsync(string title, string action, Guid? id, CancellationToken ct, Guid? customerId = null)
    {
        TInput input = id is { } key ? await FindAsync(key, ct, forEdit: true) : new TInput();
        if (!id.HasValue && input is CustomerContactInputDto contact && customerId.HasValue)
        {
            var options = await fields.OptionsAsync("Customers", ct);
            if (!options.Any(option => option.Value == customerId.Value.ToString()))
            {
                throw new NotFoundException("Customer was not found.");
            }
            contact.CustomerId = customerId;
        }
        var formFields = (await fields.FormAsync(input, ct, id)).ToList();
        if (input is NEXT.Application.DTOs.Products.ProductInputDto productInput)
        {
            var rate = productInput.ExchangeRate ?? await (exchangeRates
                ?? throw new InvalidOperationException("Exchange rate reader is not registered.")).GetUsdRateAsync(ct);
            formFields.Add(new("ExchangeRate", "ExchangeRate", FormFieldPresenter.Quantity(rate), "hidden"));
        }
        if (id.HasValue && input is IVersionedRecordDto versioned)
        {
            formFields.Add(new("RowVersion", "RowVersion", Convert.ToBase64String(versioned.RowVersion), "hidden"));
        }
        return new(title, action, formFields);
    }

    private async Task<TDto> FindAsync(Guid id, CancellationToken ct, bool forEdit = false) =>
        await sender.Send(new GetEntityByIdQuery<TDto>(id, forEdit), ct) ?? throw new NotFoundException("Record was not found.");

    private static string[] SortFields(PaginationQuery query) => query.GetSortFields() is { Count: > 0 } names
        ? names.ToArray()
        : typeof(TDto).GetProperties().Where(p => p.Name is "Name" or "Code" or "Phone" or "Email" or "Symbol")
            .Select(p => p.Name).Append("CreatedAt").ToArray();

    private static string Name(TDto item) => typeof(TInput).GetProperty("Name")?.GetValue(item)?.ToString() ?? typeof(TDto).Name;
    private static RecordIdentityDto Identity(TDto item, Func<string, object?, string> route) =>
        new(Name(item), item is IProfileImageInput { AvatarUrl.Length: > 0 } ? route("Avatar", new
        {
            id = item.Id
        }) : null);
    private static ColorBadgeDto? Badge(TDto item) => item switch
    {
        TagDto tag => new(tag.Name, tag.Color),
        ITaggedRecordDto { TagName: { } name } tag => new(name, tag.TagColor ?? "#778195"),
        _ => null
    };
}
