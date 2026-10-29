namespace NEXT.Application.Abstractions.Identity;

public sealed record IdentityOperationResult(bool Succeeded, IReadOnlyList<IdentityOperationError> Errors)
{
    public static IdentityOperationResult Success { get; } = new(true, Array.Empty<IdentityOperationError>());
    public static IdentityOperationResult Failure(IEnumerable<IdentityOperationError> errors) => new(false, errors.ToArray());
}