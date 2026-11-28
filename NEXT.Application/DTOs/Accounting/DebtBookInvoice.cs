using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;

public sealed record DebtBookInvoice(Guid Id, string Number, DateTimeOffset Date, decimal Total, decimal Discount, decimal Paid, decimal Remaining, IReadOnlyList<DebtBookLine> Lines);
