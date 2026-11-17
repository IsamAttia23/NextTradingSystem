using NEXT.Application.DTOs.Presentation;
namespace NEXT.web.Models;

public sealed record FieldOption(string Value, string Label, string? Color = null, string? BaseUnit = null, string? PackagingUnit = null, int? UnitsPerPackage = null);
