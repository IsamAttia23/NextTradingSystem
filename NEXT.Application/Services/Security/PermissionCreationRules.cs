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
