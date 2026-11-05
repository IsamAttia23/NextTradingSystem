using NEXT.Application.DTOs.Suppliers;
using NEXT.Application.Abstractions.Persistence;

using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Features.Common.Validation;

namespace NEXT.Application.Features.Suppliers;

public sealed class SupplierRules(IRepository<Tag> tags, IRepository<Supplier> suppliers) : IEntityRules<Supplier, SupplierInputDto>
{
    public void Normalize(SupplierInputDto input) => input.Code = string.IsNullOrWhiteSpace(input.Code) ? null : input.Code.Trim();

    public async Task ValidateAsync(Guid? id, SupplierInputDto input, CancellationToken ct)
    {
        var normalized = string.IsNullOrWhiteSpace(input.Code) ? null : input.Code.Trim();
        await ReferenceGuard.Require(tags, x => x.Id == input.TagId, "TagId", ct);
        if (normalized is not null
            && await suppliers.AnyAsync(x => x.Id != id
            && x.Code == normalized, ct))
        {
            ReferenceGuard.Fail("Code", "Code already exists.");
        }
    }
}
