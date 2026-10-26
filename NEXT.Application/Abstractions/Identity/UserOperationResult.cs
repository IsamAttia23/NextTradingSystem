namespace NEXT.Application.Abstractions.Identity;

/// <summary>Framework-independent result for a user-management command.</summary>
public sealed record UserOperationError(string Code, string Description);

public sealed record UserOperationResult(bool Succeeded, IReadOnlyList<UserOperationError> Errors)
{
    public static UserOperationResult Success { get; } = new(true, Array.Empty<UserOperationError>());

    public static UserOperationResult Failure(IEnumerable<UserOperationError> errors) =>
        new(false, errors.ToArray());
}
