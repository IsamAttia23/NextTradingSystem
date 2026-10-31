namespace NEXT.Application.DTOs.Presentation;

public sealed record RelatedRecordDto(Guid Id, IReadOnlyList<DetailFieldDto> Fields);
