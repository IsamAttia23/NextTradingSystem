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
}
