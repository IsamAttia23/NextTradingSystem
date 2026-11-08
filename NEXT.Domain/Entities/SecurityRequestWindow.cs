namespace NEXT.Domain.Entities;

public sealed class SecurityRequestWindow
{
    public string Key { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset LastRequestAt { get; set; }
    public int Count { get; set; }
}

