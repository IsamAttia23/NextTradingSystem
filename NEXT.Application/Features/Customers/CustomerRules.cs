using NEXT.Application.DTOs.Customers;
using NEXT.Application.Abstractions.Persistence;

using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Features.Common.Validation;
namespace NEXT.Application.Features.Customers;
public sealed class CustomerRules(IRepository<Tag> tags) : IEntityRules<Customer, CustomerInputDto>
{
    public Task ValidateAsync(Guid? id, CustomerInputDto input, CancellationToken ct) => ReferenceGuard.Require(tags, x => x.Id == input.TagId, "TagId", ct);
}
