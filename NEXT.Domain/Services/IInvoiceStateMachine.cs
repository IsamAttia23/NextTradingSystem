using NEXT.Domain.Enums;

namespace NEXT.Domain.Services;

public interface IInvoiceStateMachine
{
    bool CanTransition(InvoiceStatus from, InvoiceStatus to);
    IReadOnlyList<InvoiceStatus> GetAllowedTransitions(InvoiceStatus from);
    InvoiceStatus Transition(InvoiceStatus from, InvoiceStatus to, string? reason = null);
}
