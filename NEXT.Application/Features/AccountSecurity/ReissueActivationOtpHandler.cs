using MediatR;
using NEXT.Application.Abstractions.Identity;

namespace NEXT.Application.Features.AccountSecurity;

public sealed class ReissueActivationOtpHandler(IUserManagementService users) : IRequestHandler<ReissueActivationOtpCommand, UserOperationResult>
{
    public Task<UserOperationResult> Handle(ReissueActivationOtpCommand request, CancellationToken ct) =>
        users.ReissueActivationOtpAsync(request.UserId, ct);
}
