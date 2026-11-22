using NEXT.Domain.Enums;

namespace NEXT.Domain.Services;

public sealed class InvoiceStateMachine : IInvoiceStateMachine
{
    private static readonly IReadOnlyDictionary<InvoiceStatus, IReadOnlyList<InvoiceStatus>> Rules =
        new Dictionary<InvoiceStatus, IReadOnlyList<InvoiceStatus>>
        {
            [InvoiceStatus.Pending] = Array.AsReadOnly(new[] { InvoiceStatus.Confirmed }),
            [InvoiceStatus.Confirmed] = Array.AsReadOnly(new[] { InvoiceStatus.Shipped, InvoiceStatus.Delivered }),
            [InvoiceStatus.Shipped] = Array.AsReadOnly(new[] { InvoiceStatus.Delivered })
        };

    public IReadOnlyList<InvoiceStatus> GetAllowedTransitions(InvoiceStatus from) =>
        Rules.TryGetValue(from, out var targets) ? targets : Array.Empty<InvoiceStatus>();

    public bool CanTransition(InvoiceStatus from, InvoiceStatus to) => GetAllowedTransitions(from).Contains(to);

    public InvoiceStatus Transition(InvoiceStatus from, InvoiceStatus to, string? reason = null) =>
        CanTransition(from, to) ? to : throw new InvalidOperationException("InvoiceTransitionInvalid");
}
