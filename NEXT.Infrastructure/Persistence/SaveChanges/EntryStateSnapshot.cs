using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
namespace NEXT.Infrastructure.Persistence.SaveChanges;

internal sealed record EntryStateSnapshot(object Entity, EntityState State, PropertyValues OriginalValues,
    PropertyValues CurrentValues, IReadOnlyList<string> ModifiedProperties, IReadOnlyList<string> TemporaryProperties);
