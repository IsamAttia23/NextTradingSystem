using NEXT.Application.DTOs.Presentation;
namespace NEXT.web.Models;

public sealed record UiAction(string Label, string Url, bool Dangerous = false, bool Modal = true);
