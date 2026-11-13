using Microsoft.AspNetCore.Identity;
using NEXT.Application.Exceptions;
using NEXT.Infrastructure.Identity.Entities;

namespace NEXT.Infrastructure.Persistence.UsersManagement;
// UserManager operations without a CancellationToken must finish before their scoped context is reused/disposed.
internal static class IdentityCall
{
    public static async Task<T> CompleteAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = await StorageErrors.ExecuteAsync(operation);
        ct.ThrowIfCancellationRequested();
        return result;
    }

    public static async Task<ApplicationUser> RequireAccountAsync(UserManager<ApplicationUser> users, Guid id, CancellationToken ct)
    {
        var account = await CompleteAsync(() => users.FindByIdAsync(id.ToString()), ct);
        return account is { DeletedAt: null } ? account : throw new NotFoundException("User was not found.");
    }
}
