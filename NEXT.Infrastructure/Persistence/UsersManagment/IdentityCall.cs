namespace NEXT.Infrastructure.Persistence.UsersManagment;
// UserManager operations without a CancellationToken must finish before their scoped context is reused/disposed.
internal static class IdentityCall
{
    public static async Task<T> CompleteAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = await operation();
        ct.ThrowIfCancellationRequested();
        return result;
    }
}
