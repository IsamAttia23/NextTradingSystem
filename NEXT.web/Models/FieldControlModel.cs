using NEXT.Application.DTOs.Presentation;

namespace NEXT.web.Models;

public sealed record FieldControlModel(FormField Field, string Id, string EmptyOption, string? Placeholder = null,
    bool Disabled = false);
