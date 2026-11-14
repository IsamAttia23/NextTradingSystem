using NEXT.web.Models;
using System.Reflection;
using MediatR;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Common;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Common.CQRS;

namespace NEXT.web.Services;

public class EntityPresenter<TDto, TInput>(ISender sender, TablePresenter tables, FormFieldPresenter fields)
    where TDto : class, IRecordDto where TInput : class, new()
{
    protected ISender Sender => sender;
    protected FormFieldPresenter Fields => fields;
    protected virtual bool ShowActivity => true;
    protected virtual string BadgeColumn => "TagId";

    protected virtual IReadOnlyList<string> TableColumns =>
        ["Name", "Code", "Phone", "Email", "IsActive", "Symbol", "Color"];

    protected virtual string DisplayCell(TDto item, PropertyInfo column) =>
        FormFieldPresenter.Display(column.GetValue(item), column.Name);

    protected virtual TableRow CustomizeRow(TDto item, TableRow row) => row;

    public async Task<TableModel> TableAsync(string title, Func<string, object?, string> route, PaginationQuery query, CancellationToken ct)
    {
        var pagedList = await sender.Send(new GetEntitiesQuery<TDto>(query), ct);
        var names = TableColumns;
        var columns = typeof(TDto).GetProperties().Where(property => names.Contains(property.Name)).Take(5).ToArray();
        var tagged = typeof(ITaggedRecordDto).IsAssignableFrom(typeof(TDto));
        var labels = columns.Select(x => x.Name == "IsActive" ? "Status" : x.Name).ToList();
        if (tagged)
        {
            labels.Add("TagId");
        }
        return await tables.BuildAsync(title, labels, pagedList, query,
                    item => CustomizeRow(item, Row(item, columns, tagged, route, query)),
                    SortFields(query).Select(name => new FieldOption(name, name)).ToArray(), ct, new("Create", route("Create", null)));
    }

    private TableRow Row(TDto item, PropertyInfo[] columns, bool tagged,
        Func<string, object?, string> route, PaginationQuery query) =>
        new(columns.Select(p => DisplayCell(item, p)).Concat(tagged ? [""] : Array.Empty<string>()).ToArray(),
            [new("Details", route("Details", new { id = item.Id, returnUrl = route("Index", query) }), Modal: false),
             new("Edit", route("Edit", new { id = item.Id })), new("Delete", route("Delete", new { id = item.Id }), true)],
            Badge(item), Identity(item, route), BadgeColumn: BadgeColumn);

    public async Task<RecordDetailsDto> DetailsAsync(string section, Guid id, string? kind, int page,
        Func<string, object?, string> route, CancellationToken ct)
    {
        var item = await FindAsync(id, ct);
        var details = await BuildDetailsAsync(item, ct);
        var activity = ShowActivity
            ? await sender.Send(new GetEntityActivityQuery<TDto>(id, kind, page), ct)
            : new ActivityPageDto([], null, [], 1, 0, 0);
        return new(section, Name(item), details, Identity(item, route), Badge(item), activity,
            route("Edit", new
            {
                id
            }), route("Index", null),
            RelatedCreateAction(id, route), ShowActivity);
    }

    public async Task<ModalForm> FormAsync(string title, string action, Guid? id, CancellationToken ct, Guid? relatedId = null)
    {
        var input = id is { } key ? await LoadEditInputAsync(key, ct) : await CreateInputAsync(relatedId, ct);
        var formFields = await BuildFormFieldsAsync(input, id, ct);
        if (id.HasValue && input is IVersionedRecordDto versioned)
        {
            formFields.Add(new("RowVersion", "RowVersion", Convert.ToBase64String(versioned.RowVersion), "hidden"));
        }
        return new(title, action, formFields);
    }

    protected virtual Task<TInput> CreateInputAsync(Guid? relatedId, CancellationToken ct) => Task.FromResult(new TInput());

    protected virtual Task<IReadOnlyList<DetailFieldDto>> BuildDetailsAsync(TDto item, CancellationToken ct) =>
        fields.DetailsAsync(item, ct);

    protected virtual UiAction? RelatedCreateAction(Guid id, Func<string, object?, string> route) => null;

    protected virtual async Task<TInput> LoadEditInputAsync(Guid id, CancellationToken ct) =>
        await FindAsync(id, ct) is TInput input
            ? input
            : throw new InvalidOperationException("The entity requires a separate edit input.");

    protected virtual async Task<List<FormField>> BuildFormFieldsAsync(TInput input, Guid? id, CancellationToken ct) =>
        (await fields.FormAsync(input, ct, id)).ToList();

    private async Task<TDto> FindAsync(Guid id, CancellationToken ct) =>
        await sender.Send(new GetEntityByIdQuery<TDto>(id), ct) ?? throw new NotFoundException("Record was not found.");

    private static string[] SortFields(PaginationQuery query) => query.GetSortFields() is { Count: > 0 } names
        ? names.ToArray()
        : typeof(TDto).GetProperties().Where(p => p.Name is "Name" or "Code" or "Phone" or "Email" or "Symbol")
            .Select(p => p.Name).Append("CreatedAt").ToArray();

    private static string Name(TDto item) => typeof(TDto).GetProperty("Name")?.GetValue(item)?.ToString() ?? typeof(TDto).Name;
    private static RecordIdentityDto Identity(TDto item, Func<string, object?, string> route) =>
        new(Name(item), item is IProfileImageInput { AvatarUrl.Length: > 0 } ? route("Avatar", new
        {
            id = item.Id
        }) : null);
    protected virtual ColorBadgeDto? Badge(TDto item) => item switch
    {
        ITaggedRecordDto { TagName: { } name } tag => new(name, tag.TagColor ?? "#778195"),
        _ => null
    };
}
