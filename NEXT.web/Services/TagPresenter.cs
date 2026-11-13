using MediatR;
using NEXT.Application.DTOs.Tags;
using NEXT.web.Models;

namespace NEXT.web.Services;

public sealed class TagPresenter(ISender sender, TablePresenter tables, FormFieldPresenter fields)
    : EntityPresenter<TagDto, TagInputDto>(sender, tables, fields)
{
    protected override string BadgeColumn => nameof(TagDto.Name);
    protected override IReadOnlyList<string> TableColumns => [nameof(TagDto.Name)];
    protected override ColorBadgeDto Badge(TagDto item) => new(item.Name, item.Color);
}
