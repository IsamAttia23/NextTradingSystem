using MediatR;
using NEXT.Application.Abstractions.Identity;

namespace NEXT.Application.Features.AccountSecurity;

public sealed class UnlockUserHandler(IUserManagementService users) : IRequestHandler<UnlockUserCommand, UserOperationResult>
{
    public Task<UserOperationResult> Handle(UnlockUserCommand request, CancellationToken ct) =>
        users.UnlockUserAsync(request.UserId, ct);
}
