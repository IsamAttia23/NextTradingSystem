using MediatR;
using NEXT.Application.Abstractions.Identity;

namespace NEXT.Application.Features.ApplicationUsers.Commands;

public sealed record ReplaceUserRolesCommand(Guid UserId, IReadOnlyList<string> Roles, IReadOnlyList<string> ExpectedRoles)
    : IRequest<UserOperationResult>;
