using NEXT.Application.DTOs.Presentation;
namespace NEXT.web.Models;

public sealed record ModalForm(string Title, string Action, IReadOnlyList<FormField> Fields,
    string Submit = "Save", string? Message = null, bool Dangerous = false);
