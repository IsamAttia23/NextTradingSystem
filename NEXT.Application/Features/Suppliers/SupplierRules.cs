using NEXT.Application.DTOs.Suppliers;
using NEXT.Application.Abstractions.Persistence;

using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Features.Common.Validation;
namespace NEXT.Application.Features.Suppliers;
public sealed class SupplierRules(IRepository<Tag> tags, IRepository<Supplier> suppliers) : IEntityRules<Supplier, SupplierInputDto>
{
    public async Task ValidateAsync(Guid? id, SupplierInputDto input, CancellationToken ct)
    {
        await ReferenceGuard.Require(tags, x => x.Id == input.TagId, "TagId", ct);
        input.Code = string.IsNullOrWhiteSpace(input.Code) ? null : input.Code.Trim();
        if (input.Code is not null && await suppliers.AnyAsync(x => x.Id != id && x.Code == input.Code, ct)) ReferenceGuard.Fail("Code", "Code already exists.");
    }
}
