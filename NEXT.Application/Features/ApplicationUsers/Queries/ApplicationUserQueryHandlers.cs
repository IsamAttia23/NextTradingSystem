using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Features.ApplicationUsers.Mapping;
using NEXT.Domain.Entities;
using System.Security.Claims;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed class GetApplicationUserByIdHandler(IUserQueryService users) : IRequestHandler<GetApplicationUserByIdQuery, UserDto?>
{
    public async Task<UserDto?> Handle(GetApplicationUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await users.FindUserAsync(request.UserId);
        if (user is null) return null;
        var roles = await users.GetUserRoles(user);
        return ApplicationUserDtoMapper.ToDto(user, roles.ToArray());
    }
}

public sealed class GetApplicationUserByEmailHandler(IUserQueryService users) : IRequestHandler<GetApplicationUserByEmailQuery, UserDto?>
{
    public async Task<UserDto?> Handle(GetApplicationUserByEmailQuery request, CancellationToken cancellationToken)
    {
        var user = await users.FindUserByEmailAsync(request.Email);
        if (user is null) return null;
        var roles = await users.GetUserRoles(user);
        return ApplicationUserDtoMapper.ToDto(user, roles.ToArray());
    }
}

public sealed class GetApplicationUserByNameHandler(IUserQueryService users) : IRequestHandler<GetApplicationUserByNameQuery, UserDto?>
{
    public async Task<UserDto?> Handle(GetApplicationUserByNameQuery request, CancellationToken cancellationToken)
    {
        var user = await users.FindUserByNameAsync(request.UserName);
        return user is null ? null : ApplicationUserDtoMapper.ToDto(user);
    }
}

public sealed class GetApplicationUsersHandler(IUserQueryService users) : IRequestHandler<GetApplicationUsersQuery, IEnumerable<UserResponseDto>>
{
    public async Task<IEnumerable<UserResponseDto>> Handle(GetApplicationUsersQuery request, CancellationToken cancellationToken)
    {
        var usersList = await users.GetUsersAsync(cancellationToken);
        return usersList.Select(ApplicationUserDtoMapper.ToResponseDto).ToArray();
    }
}

public sealed class CheckApplicationUserPasswordHandler(IUserQueryService users) : IRequestHandler<CheckApplicationUserPasswordQuery, bool>
{
    public Task<bool> Handle(CheckApplicationUserPasswordQuery request, CancellationToken cancellationToken) =>
        users.CheckPasswordAsync(request.User, request.Password);
}

public sealed class GetApplicationUserClaimsHandler(IUserQueryService users) : IRequestHandler<GetApplicationUserClaimsQuery, IEnumerable<Claim>>
{
    public Task<IEnumerable<Claim>> Handle(GetApplicationUserClaimsQuery request, CancellationToken cancellationToken) =>
        users.GetUserClaims(request.User);
}

public sealed class GetApplicationUserRolesHandler(IUserQueryService users) : IRequestHandler<GetApplicationUserRolesQuery, IList<string>>
{
    public Task<IList<string>> Handle(GetApplicationUserRolesQuery request, CancellationToken cancellationToken) =>
        users.GetUserRoles(request.User);
}
