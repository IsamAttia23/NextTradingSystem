using Mapster;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.ApplicationUsers.Mapping;

public sealed class ApplicationUserMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ApplicationUser, UserResponseDto>()
            .Map(destination => destination.HasRole, source => source.UserRoles.Any());

        config.NewConfig<ApplicationUser, UserDto>()
            .Map(destination => destination.Roles, source => source.UserRoles.Select(role => role.Role.Name!).ToArray());

        config.NewConfig<UserCreationDto, ApplicationUser>()
            .Map(destination => destination.FullName, source => source.FullName.Trim())
            .Map(destination => destination.UserName, source => source.UserName.Trim())
            .Map(destination => destination.Email, source => source.Email.Trim())
            .Map(destination => destination.PhoneNumber, source => source.PhoneNumber == null ? null : source.PhoneNumber.Trim())
            .Map(destination => destination.AvatarUrl, source => source.AvatarUrl)
            .Map(destination => destination.CreatedOn, _ => DateTimeOffset.UtcNow)
            .Ignore(destination => destination.Id)
            .Ignore(destination => destination.PasswordHash)
            .Ignore(destination => destination.SecurityStamp)
            .Ignore(destination => destination.ConcurrencyStamp);

        config.NewConfig<UserUpdateDto, ApplicationUser>()
            .Map(destination => destination.FullName, source => source.FullName.Trim())
            .Map(destination => destination.UserName, source => source.UserName.Trim())
            .Map(destination => destination.AvatarUrl, source => source.AvatarUrl)
            .Map(destination => destination.BranchId, source => source.BranchId)
            .Map(destination => destination.IsActive, source => source.IsActive)
            .Map(destination => destination.UpdatedOn, _ => DateTimeOffset.UtcNow)
            .IgnoreNullValues(true)
            .IgnoreNonMapped(true);
    }
}
