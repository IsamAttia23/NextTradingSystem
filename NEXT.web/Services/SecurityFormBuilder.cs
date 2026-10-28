using NEXT.Application.DTOs.Security;
using NEXT.web.Models;

namespace NEXT.web.Services;

/// <summary>Builds role and permission presentation models without HTTP dependencies.</summary>
public static class SecurityFormBuilder
{
    public static ModalForm Role(string title, string action, RoleDto? role = null) =>
        new(title, action,
        [new("Name", "Name", role?.Name, Required: true, MaxLength: 50),
         new("Description", "Description", role?.Description, "textarea", MaxLength: 500)]);

    public static ModalForm Permission(string title, string action, PermissionDto? item = null) =>
        new(title, action,
        [new("Code", "Code", item?.Code, Required: true, MaxLength: 120),
         new("Module", "Module", item?.Module, Required: true, MaxLength: 80),
         new("Description", "Description", item?.Description, "textarea", MaxLength: 500)]);
}
