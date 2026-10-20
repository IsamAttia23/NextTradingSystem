using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for UnitOfMeasure; inherits common CRUD behavior from the generic repository.</summary>
public sealed class UnitOfMeasureRepository(NEXTDbContext context) : TRepository<UnitOfMeasure, Guid>(context), IUnitOfMeasureRepository
{

}
