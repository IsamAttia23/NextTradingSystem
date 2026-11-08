using MediatR;
using NEXT.Application.Abstractions.Identity;

namespace NEXT.Application.Features.AccountSecurity;

public sealed record FirstLoginCommand(string UserName, string Password) : IRequest<LoginPreparation>;
