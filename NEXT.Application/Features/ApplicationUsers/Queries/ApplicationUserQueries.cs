using System.Security.Claims;
using MediatR;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed record GetApplicationUserByIdQuery(string UserId) : IRequest<ApplicationUser?>;
public sealed record GetApplicationUserByEmailQuery(string Email) : IRequest<ApplicationUser?>;
public sealed record GetApplicationUserByNameQuery(string UserName) : IRequest<ApplicationUser?>;
public sealed record GetApplicationUsersQuery : IRequest<IEnumerable<ApplicationUser>>;
public sealed record CheckApplicationUserPasswordQuery(ApplicationUser User, string Password) : IRequest<bool>;
public sealed record GetApplicationUserClaimsQuery(ApplicationUser User) : IRequest<IEnumerable<Claim>>;
public sealed record GetApplicationUserRolesQuery(ApplicationUser User) : IRequest<IList<string>>;
