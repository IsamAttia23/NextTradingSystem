using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for Account; inherits common CRUD behavior from the generic repository.</summary>
public sealed class AccountRepository(NEXTDbContext context) : KeyRepository<Account, Guid>(context), IAccountRepository
{
    public Task<Account?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) => context.Accounts.AsNoTracking().FirstOrDefaultAsync(x=>x.Code==code,cancellationToken);
    public Task<List<Account>> GetChartAsync(CancellationToken cancellationToken = default) => context.Accounts.AsNoTracking().Include(x=>x.Children).OrderBy(x=>x.Code).ToListAsync(cancellationToken);
}
