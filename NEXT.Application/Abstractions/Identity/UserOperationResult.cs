namespace NEXT.Application.Abstractions.Identity;

public sealed record UserOperationResult(bool Succeeded, IReadOnlyList<UserOperationError> Errors)
{
    public IReadOnlyList<UserOperationError> Warnings { get; init; } = [];

    public static UserOperationResult Success { get; } = new(true, Array.Empty<UserOperationError>());

    public static UserOperationResult Failure(IEnumerable<UserOperationError> errors) =>
        new(false, errors.ToArray());
}
