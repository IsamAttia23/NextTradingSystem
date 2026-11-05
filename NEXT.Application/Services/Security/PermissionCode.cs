using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Domain.Entities;

namespace NEXT.Application.Services.Security;

internal static class PermissionCode
{
    public static async Task RequireUnique(IRepository<Permission> repository, Guid? id, string code, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (await repository.AnyAsync(x => x.Id != id && x.Code.ToUpper() == normalized, ct))
        {
            throw new AppValidationException(new Dictionary<string, string[]>
            {
                ["Code"] = ["This permission code already exists."]
            });
        }
    }
}
