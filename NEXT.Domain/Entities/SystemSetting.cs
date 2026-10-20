using NEXT.Domain.Common;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class SystemSetting : object
{
    public string Key { get; set; } = string.Empty;
    public string ValueJson { get; set; } = "{}";
    public string Category { get; set; } = "General";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; }
}
