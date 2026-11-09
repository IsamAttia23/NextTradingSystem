using NEXT.Application.DTOs.Customers;
using NEXT.Application.Abstractions.Persistence;

using NEXT.Domain.Entities;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Features.Common.Validation;
using NEXT.Domain.Enums;

namespace NEXT.Application.Features.Customers;

public sealed class CustomerRules(IRepository<Tag> tags) : IEntityRules<Customer, CustomerInputDto>
{
    public void Normalize(CustomerInputDto input)
    {
        if (input.Type == CustomerType.Individual)
        {
            input.CompanyName = null;
        }
    }

    public Task ValidateAsync(Guid? id, CustomerInputDto input, CancellationToken ct) =>
        ReferenceGuard.Optional(tags, input.TagId, nameof(input.TagId), ct);
}
