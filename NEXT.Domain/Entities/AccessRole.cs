namespace NEXT.Domain.Entities;

public sealed class AccessRole
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? NormalizedName { get; set; }
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
}
