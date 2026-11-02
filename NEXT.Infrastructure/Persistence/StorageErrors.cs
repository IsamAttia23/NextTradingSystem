using Microsoft.EntityFrameworkCore;
using NEXT.Application.Exceptions;
using Npgsql;
namespace NEXT.Infrastructure.Persistence;

internal static class StorageErrors
{
    public static void ThrowTranslated(Exception error)
    {
        if (error is AppException or OperationCanceledException) return;
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is OperationCanceledException cancellation)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cancellation).Throw();
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException or PostgresException { SqlState: "23505" or "40001" or "40P01" })
                throw new ConflictException("The record changed concurrently or a unique key already exists. Reload and retry.", error);
            if (current is PostgresException { SqlState: "23503" })
                throw new RecordRelationshipException(error);
            if (current is PostgresException { SqlState: "23514" or "22003" })
                throw new UnprocessableEntityException("The operation violates a storage constraint.", error);
            if (current is TimeoutException or NpgsqlException { IsTransient: true })
                throw new DependencyUnavailableException("The data service is temporarily unavailable.", error);
        }
    }
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try { return await operation(); }
        catch (Exception error) { ThrowTranslated(error); throw; }
    }
    public static async Task ExecuteAsync(Func<Task> operation)
    {
        try { await operation(); }
        catch (Exception error) { ThrowTranslated(error); throw; }
    }
}
