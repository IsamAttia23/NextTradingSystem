using NEXT.Application.Abstractions.Identity;

namespace NEXT.Application.Features.ApplicationUsers;

internal sealed class UserOperationFailedException(UserOperationResult result) : Exception("Identity operation failed.")
{
    public UserOperationResult Result { get; } = result;
}
