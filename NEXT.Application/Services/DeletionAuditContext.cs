using NEXT.Application.Abstractions.Context;
using NEXT.Application.Exceptions;

namespace NEXT.Application.Services;

public sealed class DeletionAuditContext : IDeletionAuditContext
{
    public string? Reason { get; private set; }
    public IDisposable Begin(string reason)
    {
        var previous = Reason;
        Reason = DeletionReason.Require(reason);
        return new Scope(() => Reason = previous);
    }
    private sealed class Scope(Action restore) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            restore();
        }
    }
}
