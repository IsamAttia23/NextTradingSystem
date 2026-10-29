using NEXT.Application.DTOs.Categories;
using NEXT.Application.Abstractions.Persistence;

using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Features.Common.Validation;
namespace NEXT.Application.Features.Categories;
public sealed class CategoryRules(IKeyRepository<Category, Guid> categories) : IEntityRules<Category, CategoryInputDto>
{
    public Task ValidateAsync(Guid? id, CategoryInputDto input, CancellationToken ct) => ReferenceGuard.NoCycle(id, input.ParentId, categories, x => x.ParentId, "ParentId", ct);
}
