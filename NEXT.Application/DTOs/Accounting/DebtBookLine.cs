using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;

public sealed record DebtBookLine(Guid InvoiceId, string Name, decimal Quantity, decimal UnitPrice, decimal Total);
