using NEXT.Domain.Entities;
using NEXT.Application.DTOs.Accounting;
using NEXT.Domain.Enums;
namespace NEXT.Application.Abstractions.Persistence;
public interface IFinancialControlStore
{
    Task LockAsync(CancellationToken ct);
    Task EnsureOpenAsync(DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<FinancialPeriod>> PeriodsAsync(CancellationToken ct);
    Task<FinancialPeriod?> PeriodAsync(Guid id, CancellationToken ct);
    Task AddPeriodAsync(FinancialPeriod period, FinancialPeriodEvent audit, CancellationToken ct);
    Task AddEventAsync(FinancialPeriodEvent audit, CancellationToken ct);
    Task<IReadOnlyList<PeriodEventView>> EventsAsync(CancellationToken ct);
    Task<CashPosition> PositionAsync(AccountType account, DateOnly date, CancellationToken ct);
    Task<CashTransfer?> TransferAsync(Guid operationId, CancellationToken ct);
    Task AddTransferAsync(CashTransfer transfer, CancellationToken ct);
    Task<IReadOnlyList<CashTransferView>> TransfersAsync(CancellationToken ct);
    Task<CashReconciliation?> ReconciliationAsync(Guid operationId, CancellationToken ct);
    Task AddReconciliationAsync(CashReconciliation document, CancellationToken ct);
    Task<IReadOnlyList<CashReconciliation>> ReconciliationsAsync(CancellationToken ct);
}
