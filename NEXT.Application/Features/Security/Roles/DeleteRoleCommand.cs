using MediatR;
using NEXT.Application.DTOs.Security;

namespace NEXT.Application.Features.Security.Roles;

public sealed record DeleteRoleCommand(Guid Id, string? Reason = null) : IRequest;
