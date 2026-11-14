using MediatR;
using NEXT.Application.Abstractions.Identity;
namespace NEXT.Application.Features.ApplicationUsers.Commands;

public sealed record SetApplicationUserActiveCommand(Guid UserId, bool? IsActive, bool? ExpectedIsActive, bool? ExpectedIsLocked = null)
    : IRequest<UserOperationResult>;
