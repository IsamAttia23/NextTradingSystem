using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Domain.Entities;

namespace NEXT.Application.Services.Security;

public sealed class PermissionCreationRules(IRepository<Permission> repository) : IEntityRules<Permission, PermissionCreationDto>
{
    public Task ValidateAsync(Guid? id, PermissionCreationDto input, CancellationToken ct) => PermissionCode.RequireUnique(repository, id, input.Code, ct);
}
public sealed class PermissionUpdateRules(IRepository<Permission> repository) : IEntityRules<Permission, PermissionUpdateDto>
{
    public Task ValidateAsync(Guid? id, PermissionUpdateDto input, CancellationToken ct) => PermissionCode.RequireUnique(repository, id, input.Code, ct);
}
internal static class PermissionCode
{
    public static async Task RequireUnique(IRepository<Permission> repository, Guid? id, string code, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (await repository.AnyAsync(x => x.Id != id && x.Code.ToUpper() == normalized, ct))
            throw new AppValidationException(new Dictionary<string, string[]> { ["Code"] = ["This permission code already exists."] });
    }
}
