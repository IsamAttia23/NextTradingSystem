namespace NEXT.Application.DTOs.Presentation;
public sealed record RecordDetailsDto(
    string Section, 
    string Name,
    IReadOnlyList<DetailFieldDto> Fields,
    RecordIdentityDto Identity,
    ColorBadgeDto? Badge,
    ActivityPageDto Activity, 
    string EditUrl,
    string BackUrl
    );
