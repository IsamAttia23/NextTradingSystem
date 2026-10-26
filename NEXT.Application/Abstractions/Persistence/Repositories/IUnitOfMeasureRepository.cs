using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for UnitOfMeasure. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IUnitOfMeasureRepository : IKeyRepository<UnitOfMeasure, Guid>
{

}
