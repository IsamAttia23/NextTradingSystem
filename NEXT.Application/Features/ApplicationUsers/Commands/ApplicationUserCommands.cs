using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.ApplicationUsers.Commands;

public sealed record CreateApplicationUserCommand(UserCreationDto User) : IRequest<UserOperationResult>;
public sealed record UpdateApplicationUserCommand(string UserId, UserUpdateDto User) : IRequest<UserOperationResult>;
public sealed record ChangeApplicationUserPasswordCommand(ApplicationUser User, string CurrentPassword, string NewPassword) : IRequest<UserOperationResult>;
public sealed record SetApplicationUserNameCommand(ApplicationUser User, string UserName) : IRequest<UserOperationResult>;
public sealed record SetApplicationUserEmailCommand(ApplicationUser User, string Email) : IRequest<UserOperationResult>;
public sealed record SetApplicationUserPhoneNumberCommand(ApplicationUser User, string PhoneNumber) : IRequest<UserOperationResult>;
public sealed record AddApplicationUserToRoleCommand(ApplicationUser User, string Role) : IRequest<UserOperationResult>;
public sealed record AssignUserRoleCommand(Guid UserId, string Role) : IRequest<UserOperationResult>;
