using Mapster;
using NEXT.Application.DTOs.Security;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Security.Mapping;

public sealed class SecurityMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Permission, PermissionDto>();
        config.NewConfig<PermissionCreationDto, Permission>()
            .Map(d => d.Code, s => s.Code.Trim())
            .Map(d => d.Module, s => s.Module.Trim());
        config.NewConfig<PermissionUpdateDto, Permission>()
            .Map(d => d.Code, s => s.Code.Trim())
            .Map(d => d.Module, s => s.Module.Trim())
            .Ignore(d => d.Id)
            .Ignore(d => d.CreatedAt)
            .Ignore(d => d.CreatedBy);

        config.NewConfig<Role, RoleDto>();
        config.NewConfig<RoleCreationDto, Role>()
            .Map(d => d.Name, s => s.Name.Trim())
            .Map(d => d.NormalizedName, s => s.Name.Trim().ToUpperInvariant());
        config.NewConfig<RoleUpdateDto, Role>()
            .Map(d => d.Name, s => s.Name.Trim())
            .Map(d => d.NormalizedName, s => s.Name.Trim().ToUpperInvariant())
            .Ignore(d => d.Id)
            .Ignore(d => d.ConcurrencyStamp);

        config.NewConfig<RolePermission, RolePermissionDto>()
            .Map(d => d.RoleName, s => s.Role.Name)
            .Map(d => d.Code, s => s.Permission.Code)
            .Map(d => d.Module, s => s.Permission.Module);
    }
}