using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Exceptions;
namespace NEXT.Application.Features.ApplicationUsers;
public sealed class UserDeletionPolicy(IAdministratorLookup administrators) : IUserDeletionPolicy
{
    public async Task ValidateAsync(Guid userId, CancellationToken ct)
    {
        if (await administrators.IsLastActiveAsync(userId, ct))
            throw new UnprocessableEntityException("The last active administrator cannot be deleted.");
    }
}
