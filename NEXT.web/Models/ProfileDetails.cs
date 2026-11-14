using NEXT.Application.DTOs.Presentation;

namespace NEXT.web.Models;

public sealed record ProfileDetails(RecordIdentityDto Identity, IReadOnlyList<DetailFieldDto> Fields);
