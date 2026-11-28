namespace NEXT.Application.DTOs.Stores;

public sealed class StoreActivationInput
{
    public Guid Id { get; set; }
    public int Version { get; set; }
    public bool Active { get; set; }
}
