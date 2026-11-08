namespace NEXT.Application.Abstractions.Identity;

public sealed record LoginPreparation(LoginOutcome Outcome, string? SetupToken = null);
