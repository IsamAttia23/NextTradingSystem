using NEXT.Application.DTOs.Stores;

namespace NEXT.Infrastructure.Stores;

public sealed record StoreRegistration(Guid Id, string Name, string Slug, string DatabaseName,
    StoreState State, string OwnerName, string OwnerUserName, string OwnerEmail,
    string RequestHash, Guid CreatedBy, DateTimeOffset CreatedAt, int Version);
