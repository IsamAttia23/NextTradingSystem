using Mapster;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.DTOs.Security;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Identity.Entities;

namespace NEXT.Infrastructure.Identity;

public sealed class IdentityMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ApplicationUser, UserResponseDto>()
            .Map(destination => destination.HasRole, source => source.UserRoles.Any());
        config.NewConfig<ApplicationUser, UserDto>()
            .Map(destination => destination.Roles, source => source.UserRoles.Select(role => role.Role.Name!).ToArray());
        config.NewConfig<ApplicationUser, UserAccount>();
        config.NewConfig<Role, RoleDto>();
        config.NewConfig<Role, AccessRole>();
    }
}
