using NEXT.Application.Abstractions.Context;
using NEXT.Application.Exceptions;
namespace NEXT.Application.Services;
public static class DeletionReason
{
    public static string Require(string? reason)
    {
        var value = reason?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 500)
            throw new AppValidationException(new Dictionary<string,string[]> { ["Reason"] = [string.IsNullOrWhiteSpace(value) ? "DeletionReasonRequired" : "DeletionReasonTooLong"] });
        return value;
    }
}
/// <summary>A scoped reason, restored after each operation, for both soft and permanent deletion audit records.</summary>
public sealed class DeletionAuditContext : IDeletionAuditContext
{
    public string? Reason { get; private set; }
    public IDisposable Begin(string reason)
    {
        var previous = Reason; Reason = DeletionReason.Require(reason);
        return new Scope(() => Reason = previous);
    }
    private sealed class Scope(Action restore) : IDisposable
    {
        private bool disposed;
        public void Dispose() { if (disposed) return; disposed = true; restore(); }
    }
}
