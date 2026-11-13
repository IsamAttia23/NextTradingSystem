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
        config.NewConfig<AccessRole, RoleDto>();
        config.NewConfig<RoleCreationDto, AccessRole>()
            .Map(d => d.Name, s => s.Name.Trim());
        config.NewConfig<RoleUpdateDto, AccessRole>()
            .Map(d => d.Name, s => s.Name.Trim())
            .Ignore(d => d.Id)
            .Ignore(d => d.IsSystem);
    }
}
