using NEXT.Application.DTOs.Presentation;
namespace NEXT.web.Models;

public sealed record HeaderProfile(
    string FullName,
    bool HasAvatar,
    IReadOnlyList<DetailFieldDto> LoginHistory);
