namespace NEXT.Application.Abstractions.Context;
public interface IDeletionAuditContext
{
    string? Reason { get; }
    IDisposable Begin(string reason);
}
