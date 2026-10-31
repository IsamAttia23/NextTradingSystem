namespace NEXT.Application.DTOs.Presentation;

public sealed record ActivityPageDto(IReadOnlyList<ActivitySectionDto> Sections, string? Selected,
    IReadOnlyList<RelatedRecordDto> Records, int Page, int TotalPages, int TotalCount);
