using System.Linq.Expressions;
using NEXT.Domain.Entities;
using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

public interface ICustomerRepository : IKeyRepository<Customer, Guid>
{
    Expression<Func<Customer, bool>> BuildFilterExpression(CustomerQueryModel query);
}
