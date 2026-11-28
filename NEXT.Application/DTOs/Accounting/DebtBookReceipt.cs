using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;

public sealed record DebtBookReceipt(Guid Id, DateOnly Date, string Number, decimal Amount, PaymentMethod Method, AccountType CashAccount, Guid? InvoiceId, string? InvoiceNumber, Guid? AccountPaymentId, string? Description = null);
