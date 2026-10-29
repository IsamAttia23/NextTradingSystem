namespace NEXT.Application.Abstractions.Identity;

public enum LoginOutcome { Failed, Succeeded, RequiresTwoFactor, MissingChallenge }
