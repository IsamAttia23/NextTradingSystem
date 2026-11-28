namespace NEXT.Application.DTOs.Stores;

// Only administrator-facing information crosses the application boundary.
public sealed record TrialStoreView(Guid Id, string Name, string Slug, StoreState State,
    string OwnerName, string OwnerUserName, string OwnerEmail, int Version);
