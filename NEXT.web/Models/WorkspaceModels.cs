namespace NEXT.web.Models;

public sealed record UiAction(string Label, string Url, bool Dangerous = false);
public sealed record TableRow(IReadOnlyList<string> Cells, IReadOnlyList<UiAction> Actions);
public sealed record TableModel(string Title, string Description, IReadOnlyList<string> Columns,
    IReadOnlyList<TableRow> Rows, UiAction? Create = null, int Page = 1, int TotalPages = 1, int TotalCount = 0);
public sealed record FieldOption(string Value, string Label);
public sealed record FormField(string Name, string Label, string? Value = null, string Type = "text",
    bool Required = false, int? MaxLength = null, IReadOnlyList<FieldOption>? Options = null);
public sealed record ModalForm(string Title, string Action, IReadOnlyList<FormField> Fields,
    string Submit = "Save", string? Message = null, bool Dangerous = false);
public sealed record DashboardModel(int Users, int ActiveUsers, int Roles, int Permissions, int Assignments);
