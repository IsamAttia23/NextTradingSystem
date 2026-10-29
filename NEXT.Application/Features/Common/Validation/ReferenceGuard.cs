using NEXT.Application.Abstractions.Persistence;

using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Application.Features.Common.CQRS;
namespace NEXT.Application.Features.Common.Validation;
internal static class ReferenceGuard
{
    public static async Task Require<T>(IRepository<T> repository, System.Linq.Expressions.Expression<Func<T,bool>> predicate,
        string field, CancellationToken ct) where T : class
    {
        if (!await repository.AnyAsync(predicate, ct)) Fail(field, "Select an existing, active record.");
    }
    public static void Fail(string field, string message) => throw new AppValidationException(new Dictionary<string,string[]> { [field] = [message] });
    public static async Task NoCycle<T>(Guid? id, Guid? parent, IKeyRepository<T, Guid> repository, Func<T, Guid?> next, string field, CancellationToken ct) where T : class
    {
        var visited = new HashSet<Guid>();
        while (parent is { } ancestor)
        {
            if (ancestor == id || !visited.Add(ancestor)) Fail(field, "A circular hierarchy is not allowed.");
            var item = await repository.GetByIdAsync(ancestor, ct) ?? throw new NotFoundException("Parent was not found.");
            parent = next(item);
        }
    }
}
