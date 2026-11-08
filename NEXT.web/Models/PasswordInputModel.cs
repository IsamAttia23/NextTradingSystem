namespace NEXT.web.Models;

public sealed record PasswordInputModel(string Id, string Name, string Label,
    string Autocomplete = "new-password", bool Required = true, int? MinLength = 10,
    int MaxLength = 128, string? Placeholder = null);
