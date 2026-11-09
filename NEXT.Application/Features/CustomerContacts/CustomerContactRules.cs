using NEXT.Application.DTOs.CustomerContacts;
using NEXT.Application.Abstractions.Persistence;

using NEXT.Domain.Entities;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Features.Common.Validation;

namespace NEXT.Application.Features.CustomerContacts;

public sealed class CustomerContactRules(IRepository<Customer> customers) : IEntityRules<CustomerContact, CustomerContactInputDto>
{
    public Task ValidateAsync(Guid? id, CustomerContactInputDto input, CancellationToken ct) =>
        input.CustomerId.HasValue
            ? ReferenceGuard.Require(customers, x => x.Id == input.CustomerId && x.IsActive, nameof(input.CustomerId), ct)
            : Task.CompletedTask;
}
