using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Infrastructure.Persistence.Repositories;

public sealed class UnitOfWork(
    NEXTDbContext context,
    IServiceProvider serviceProvider) : IUnitOfWork
{
    public IRepository<TEntity> Repository<TEntity, TKey>() where TEntity : class
        => serviceProvider.GetRequiredService<IRepository<TEntity>>();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (context.Database.CurrentTransaction is not null)
        {
            var nestedResult = await operation(cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return nestedResult;
        }

        await using IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await ExecuteInTransactionAsync(async ct =>
        {
            await operation(ct);
            return true;
        }, cancellationToken);
    }
}
