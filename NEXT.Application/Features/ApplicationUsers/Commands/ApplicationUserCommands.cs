using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Identity;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.ApplicationUsers.Commands;

public sealed record CreateApplicationUserCommand(ApplicationUser User, string Password) : IRequest<IdentityResult>;
public sealed record UpdateApplicationUserCommand(ApplicationUser User) : IRequest<IdentityResult>;
public sealed record ChangeApplicationUserPasswordCommand(ApplicationUser User, string CurrentPassword, string NewPassword) : IRequest<IdentityResult>;
public sealed record SetApplicationUserNameCommand(ApplicationUser User, string UserName) : IRequest<IdentityResult>;
public sealed record SetApplicationUserEmailCommand(ApplicationUser User, string Email) : IRequest<IdentityResult>;
public sealed record SetApplicationUserPhoneNumberCommand(ApplicationUser User, string PhoneNumber) : IRequest<IdentityResult>;
public sealed record AddApplicationUserToRoleCommand(ApplicationUser User, string Role) : IRequest<IdentityResult>;
