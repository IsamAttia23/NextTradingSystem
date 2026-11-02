using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Features.ApplicationUsers;

internal static class UserOperationTransaction
{
    public static async Task<UserOperationResult> ExecuteAsync(IUnitOfWork unitOfWork,
        Func<CancellationToken, Task<UserOperationResult>> action, CancellationToken ct)
    {
        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                var result = await action(token);
                if (!result.Succeeded) throw new UserOperationFailedException(result);
                return result;
            }, ct);
        }
        catch (UserOperationFailedException failure) { return failure.Result; }
    }
}
