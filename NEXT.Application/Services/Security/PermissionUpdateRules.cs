using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Domain.Entities;

namespace NEXT.Application.Services.Security;

public sealed class PermissionUpdateRules(IRepository<Permission> repository) : IEntityRules<Permission, PermissionUpdateDto>
{
    public Task ValidateAsync(Guid? id, PermissionUpdateDto input, CancellationToken ct) => PermissionCode.RequireUnique(repository, id, input.Code, ct);
}
