using NEXT.Application.DTOs.UnitsOfMeasure;
using NEXT.Application.Abstractions.Persistence;

using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Features.Common.Validation;
namespace NEXT.Application.Features.UnitsOfMeasure;
public sealed class UnitOfMeasureRules(IKeyRepository<UnitOfMeasure, Guid> units) : IEntityRules<UnitOfMeasure, UnitOfMeasureInputDto>
{
    public Task ValidateAsync(Guid? id, UnitOfMeasureInputDto input, CancellationToken ct) => ReferenceGuard.NoCycle(id, input.BaseUnitId, units, x => x.BaseUnitId, "BaseUnitId", ct);
}
