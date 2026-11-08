using MediatR;
using NEXT.Application.Abstractions.Identity;

namespace NEXT.Application.Features.AccountSecurity;

public sealed class FirstLoginHandler(IUserManagementService users) : IRequestHandler<FirstLoginCommand, LoginPreparation>
{
    public Task<LoginPreparation> Handle(FirstLoginCommand request, CancellationToken ct) =>
        users.PrepareLoginAsync(request.UserName, request.Password, true, ct);
}
