using MediatR;
using NEXT.Application.Abstractions.Identity;

namespace NEXT.Application.Features.AccountSecurity;

public sealed class RequestPasswordResetHandler(IUserManagementService users) : IRequestHandler<RequestPasswordResetCommand, PasswordResetRequested>
{
    public async Task<PasswordResetRequested> Handle(RequestPasswordResetCommand request, CancellationToken ct) =>
        new(await users.RequestPasswordResetAsync(request.Email, ct));
}
