using NEXT.Application.DTOs.CustomerContacts;
using NEXT.Application.Abstractions.Persistence;

using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Features.Common.Validation;
namespace NEXT.Application.Features.CustomerContacts;
public sealed class CustomerContactRules(IRepository<Customer> customers) : IEntityRules<CustomerContact, CustomerContactInputDto>
{
    public Task ValidateAsync(Guid? id, CustomerContactInputDto input, CancellationToken ct) => ReferenceGuard.Require(customers, x => x.Id == input.CustomerId && x.IsActive, "CustomerId", ct);
}
