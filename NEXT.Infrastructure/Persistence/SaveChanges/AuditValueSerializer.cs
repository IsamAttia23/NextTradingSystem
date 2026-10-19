using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace NEXT.Infrastructure.Persistence.SaveChanges;

/// <summary>Serializes entity values for audit records while excluding common credentials and secrets.</summary>
public sealed class AuditValueSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
        "AuthenticatorKey",
        "RecoveryCodes"
    };

    public string Serialize(EntityEntry entry, bool originalValues)
    {
        var values = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (ShouldExclude(name))
                continue;

            values[name] = originalValues
                ? property.OriginalValue
                : property.CurrentValue;
        }

        return JsonSerializer.Serialize(values, JsonOptions);
    }

    private static bool ShouldExclude(string name) =>
        SensitivePropertyNames.Contains(name) ||
        name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Secret", StringComparison.OrdinalIgnoreCase);
}
