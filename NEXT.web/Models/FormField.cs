using NEXT.Application.DTOs.Presentation;
namespace NEXT.web.Models;

public sealed record FormField(string Name, string Label, string? Value = null, string Type = "text",
    bool Required = false, int? MaxLength = null, IReadOnlyList<FieldOption>? Options = null,
    string? VisibleWhenField = null, string? VisibleWhenValue = null, IReadOnlyList<string>? SelectedValues = null);
