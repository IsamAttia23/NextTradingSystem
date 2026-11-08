using MediatR;
using NEXT.Application.Abstractions.Identity;

namespace NEXT.Application.Features.AccountSecurity;

public sealed class ChangeVerifiedPasswordHandler(IUserManagementService users) : IRequestHandler<ChangeVerifiedPasswordCommand, UserOperationResult>
{
    public Task<UserOperationResult> Handle(ChangeVerifiedPasswordCommand request, CancellationToken ct) =>
        request.FirstLogin
            ? users.CompleteFirstLoginAsync(request.Token, request.NewPassword, ct)
            : users.ResetPasswordAsync(request.Token, request.NewPassword, ct);
}
