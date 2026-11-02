using System.Reflection;
using MediatR;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Common;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Common.CQRS;

namespace NEXT.web.Services;

public sealed class EntityPresenter<TDto, TInput>(ISender sender, TablePresenter tables, FormFieldPresenter fields)
    where TDto : class, TInput, IRecordDto where TInput : class, new()
{
    public async Task<TableModel> TableAsync(string title, Func<string, object?, string> route, PaginationQuery query, CancellationToken ct)
    {
        var pagedList = await sender.Send(new GetEntitiesQuery<TDto>(query), ct);
        var columns = typeof(TInput).GetProperties()
            .Where(p => p.Name is "Name" or "Code" or "Phone" or "Email" or "IsActive" or "Symbol" or "Color").Take(5).ToArray();
        var tagged = typeof(ITaggedRecordDto).IsAssignableFrom(typeof(TDto));
        var labels = columns.Select(x => x.Name == "IsActive" ? "Status" : x.Name).ToList();
        if (tagged) labels.Add("TagId");
        return await tables.BuildAsync(title, labels, pagedList, query,
            item => Row(item, columns, tagged, route, query),
            SortFields(query).Select(name => new FieldOption(name, name)).ToArray(), ct, new("Create", route("Create", null)));
    }

    private static TableRow Row(TDto item, PropertyInfo[] columns, bool tagged,
        Func<string, object?, string> route, PaginationQuery query) =>
        new(columns.Select(p => FormFieldPresenter.Display(p.GetValue(item))).Concat(tagged ? [""] : Array.Empty<string>()).ToArray(),
            [new("Details", route("Details", new { id = item.Id, returnUrl = route("Index", query) }), Modal: false),
             new("Edit", route("Edit", new { id = item.Id })), new("Delete", route("Delete", new { id = item.Id }), true)],
            Badge(item), Identity(item, route));

    public async Task<RecordDetailsDto> DetailsAsync(string section, Guid id, string? kind, int page,
        Func<string, object?, string> route, CancellationToken ct)
    {
        var item = await FindAsync(id, ct);
        var details = await fields.DetailsAsync(item, ct);
        var activity = await sender.Send(new GetEntityActivityQuery<TDto>(id, kind, page), ct);
        return new(section, Name(item), details, Identity(item, route), Badge(item), activity,
            route("Edit", new { id }), route("Index", null));
    }

    public async Task<ModalForm> FormAsync(string title, string action, Guid? id, CancellationToken ct)
    {
        TInput input = id is { } key ? await FindAsync(key, ct) : new TInput();
        return new(title, action, await fields.FormAsync(input, ct, id));
    }

    private async Task<TDto> FindAsync(Guid id, CancellationToken ct) =>
        await sender.Send(new GetEntityByIdQuery<TDto>(id), ct) ?? throw new NotFoundException("Record was not found.");

    private static string[] SortFields(PaginationQuery query) => query.GetSortFields() is { Count: > 0 } names
        ? names.ToArray()
        : typeof(TDto).GetProperties().Where(p => p.Name is "Name" or "Code" or "Phone" or "Email" or "Symbol")
            .Select(p => p.Name).Append("CreatedAt").ToArray();

    private static string Name(TDto item) => typeof(TInput).GetProperty("Name")?.GetValue(item)?.ToString() ?? typeof(TDto).Name;
    private static RecordIdentityDto Identity(TDto item, Func<string, object?, string> route) =>
        new(Name(item), item is IProfileImageInput { AvatarUrl.Length: > 0 } ? route("Avatar", new { id = item.Id }) : null);
    private static ColorBadgeDto? Badge(TDto item) => item is ITaggedRecordDto { TagName: { } name } tag
        ? new(name, tag.TagColor ?? "#778195") : null;
}
