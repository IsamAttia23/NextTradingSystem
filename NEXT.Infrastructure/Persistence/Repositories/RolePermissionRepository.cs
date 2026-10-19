using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for RolePermission; inherits common CRUD behavior from the generic repository.</summary>
public sealed class RolePermissionRepository(NEXTDbContext context) : Repository<RolePermission, object[]>(context), IRolePermissionRepository
{

}
