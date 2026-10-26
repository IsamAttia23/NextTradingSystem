using NEXT.Domain.Entities;
using NEXT.Domain.QueryModels;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for Payment. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IPaymentRepository : IKeyRepository<Payment, Guid>
{
    IQueryable<Payment> SearchAsync(PaymentQueryModel queryModel, CancellationToken cancellationToken = default);
}
