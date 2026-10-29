using NEXT.Application.DTOs.Warehouses;
using NEXT.Application.Abstractions.Persistence;

using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Features.Common.Validation;
namespace NEXT.Application.Features.Warehouses;
public sealed class WarehouseRules(IRepository<Branch> branches, IRepository<Warehouse> warehouses) : IEntityRules<Warehouse, WarehouseInputDto>
{
    public async Task ValidateAsync(Guid? id, WarehouseInputDto input, CancellationToken ct)
    {
        if (input.BranchId is {} branch) await ReferenceGuard.Require(branches, x => x.Id == branch && x.IsActive, "BranchId", ct);
        if (input.IsDefault && !input.IsActive) ReferenceGuard.Fail("IsDefault", "A default warehouse must be active.");
        if (await warehouses.AnyAsync(x => x.Id != id && x.Code == input.Code, ct)) ReferenceGuard.Fail("Code", "Code already exists.");
        if (input.IsDefault && await warehouses.AnyAsync(x => x.Id != id && x.IsDefault, ct)) ReferenceGuard.Fail("IsDefault", "A default warehouse already exists.");
    }
}
