using MediatR;
using NEXT.Application.Abstractions.Identity;

namespace NEXT.Application.Features.AccountSecurity;

public sealed record RequestPasswordResetCommand(string Email) : IRequest<PasswordResetRequested>;
