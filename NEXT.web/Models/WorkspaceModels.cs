using System.ComponentModel.DataAnnotations;

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

public sealed class RoleInput
{
    [Required, StringLength(50)] public string Name { get; set; } = "";
    [StringLength(500)] public string? Description { get; set; }
}
public sealed class PermissionInput
{
    [Required, StringLength(120)] public string Code { get; set; } = "";
    [Required, StringLength(80)] public string Module { get; set; } = "";
    [StringLength(500)] public string? Description { get; set; }
}
public sealed class UserInput
{
    public IFormFile? Avatar { get; set; }
    [Required, StringLength(200)] public string FullName { get; set; } = "";
    [Required, StringLength(256)] public string UserName { get; set; } = "";
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Phone, StringLength(32)] public string? PhoneNumber { get; set; }
    public Guid BranchId { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class CreateUserInput
{
    public UserInput Profile { get; set; } = new();
    [Required, MinLength(10), StringLength(128)] public string Password { get; set; } = "";
}
public sealed class LoginInput
{
    [Required, StringLength(256)] public string UserName { get; set; } = "";
    [Required] public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
public sealed class VerifyLoginInput
{
    [Required, StringLength(32)] public string Code { get; set; } = "";
    public bool UseRecoveryCode { get; set; }
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
