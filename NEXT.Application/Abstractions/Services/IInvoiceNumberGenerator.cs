namespace NEXT.Application.Abstractions.Services;

public interface IInvoiceNumberGenerator
{
    Task<string> NextAsync(DateOnly date, CancellationToken ct);
}
