namespace NEXT.Application.DTOs.Presentation;
public sealed record DetailFieldDto(string Label, string Value, bool TranslateValue = false);
public sealed record RelatedRecordDto(Guid Id, IReadOnlyList<DetailFieldDto> Fields);
public sealed record ActivitySectionDto(string Key, string Title, int Count);
public sealed record ActivityPageDto(IReadOnlyList<ActivitySectionDto> Sections, string? Selected,
    IReadOnlyList<RelatedRecordDto> Records, int Page, int TotalPages, int TotalCount);
public sealed record RecordDetailsDto(string Section, string Name, IReadOnlyList<DetailFieldDto> Fields,
    RecordIdentityDto Identity, ColorBadgeDto? Badge, ActivityPageDto Activity, string EditUrl, string BackUrl);
public sealed record PagerDto(int Page, int TotalPages, int TotalCount, string PageKey = "PageNumber", int PageSize = 10, bool AllowPageSize = false);
