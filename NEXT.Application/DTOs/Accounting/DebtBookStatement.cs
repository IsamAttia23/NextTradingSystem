using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;

public sealed record DebtBookStatement(DebtBookAccount Account, IReadOnlyList<DebtBookInvoice> Invoices, IReadOnlyList<DebtBookReceipt> Receipts, IReadOnlyList<DebtBookOpening> Openings, decimal Balance)
{
    public decimal Total => Invoices.Sum(i => i.Total) + Openings.Sum(o => o.Amount);
    public decimal Paid => Receipts.Sum(p => p.Amount);
}
