using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

public interface IAccountRepository : IKeyRepository<Account, Guid>
{
    Task<Account?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<List<Account>> GetChartAsync(CancellationToken cancellationToken = default);
}
