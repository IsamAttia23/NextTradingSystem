using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.ApplicationUsers.Commands;

public sealed record DeleteApplicationUserCommand(Guid UserId, string? Reason = null) : IRequest<UserOperationResult>;
