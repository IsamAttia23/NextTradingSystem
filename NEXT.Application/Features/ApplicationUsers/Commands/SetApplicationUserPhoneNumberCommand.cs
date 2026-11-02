using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.ApplicationUsers.Commands;

public sealed record SetApplicationUserPhoneNumberCommand(Guid UserId, string PhoneNumber) : IRequest<UserOperationResult>;
