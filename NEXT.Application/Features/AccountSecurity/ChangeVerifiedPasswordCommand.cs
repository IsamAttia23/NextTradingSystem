using MediatR;
using NEXT.Application.Abstractions.Identity;

namespace NEXT.Application.Features.AccountSecurity;

public sealed record ChangeVerifiedPasswordCommand(string Token, string NewPassword, bool FirstLogin) : IRequest<UserOperationResult>;
