using System.Security.Claims;
using MediatR;
using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Identity;

namespace NEXT.Application.Features.ApplicationUsers.Commands;

public sealed record CreateApplicationUserCommand(ApplicationUser User, string Password) : IRequest<UserOperationResult>;
public sealed record UpdateApplicationUserCommand(ApplicationUser User) : IRequest<UserOperationResult>;
public sealed record ChangeApplicationUserPasswordCommand(ApplicationUser User, string CurrentPassword, string NewPassword) : IRequest<UserOperationResult>;
public sealed record SetApplicationUserNameCommand(ApplicationUser User, string UserName) : IRequest<UserOperationResult>;
public sealed record SetApplicationUserEmailCommand(ApplicationUser User, string Email) : IRequest<UserOperationResult>;
public sealed record SetApplicationUserPhoneNumberCommand(ApplicationUser User, string PhoneNumber) : IRequest<UserOperationResult>;
public sealed record AddApplicationUserToRoleCommand(ApplicationUser User, string Role) : IRequest<UserOperationResult>;
