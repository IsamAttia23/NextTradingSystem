using MediatR;
using NEXT.Application.Abstractions.Identity;

namespace NEXT.Application.Features.AccountSecurity;

public sealed record VerifyPasswordResetOtpCommand(Guid ChallengeId, string Code) : IRequest<OtpVerification>;
