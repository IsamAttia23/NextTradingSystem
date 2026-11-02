using System.Linq.Expressions;
using NEXT.Domain.Entities;
using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

public interface IPaymentRepository : IKeyRepository<Payment, Guid>
{
    Expression<Func<Payment, bool>> BuildFilterExpression(PaymentQueryModel query);
}
