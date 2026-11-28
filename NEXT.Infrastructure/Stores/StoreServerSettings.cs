using Microsoft.Extensions.Configuration;
using Npgsql;

namespace NEXT.Infrastructure.Stores;

public sealed class StoreServerSettings(IConfiguration configuration)
{
    public bool Enabled => configuration.GetValue("Stores:Enabled", true);
    internal string OwnerRole => configuration.GetSection("Security:AdministratorRoles").Get<string[]>()?.FirstOrDefault() ?? "Admin";
    internal string RootConnection => configuration.GetConnectionString("NEXTDatabase")
        ?? throw new InvalidOperationException("Root database configuration is missing.");
    internal string ManagementConnection => configuration.GetConnectionString("StoreProvisioning") ?? RootConnection;
    public static string DatabaseName(Guid id) => "nts_trial_" + id.ToString("N");

    internal string ConnectionFor(StoreRegistration store, bool management = false)
    {
        var builder = new NpgsqlConnectionStringBuilder(management ? ManagementConnection : RootConnection);
        if (store.Id == Guid.Empty || store.DatabaseName != DatabaseName(store.Id) || store.DatabaseName == builder.Database)
            throw new InvalidOperationException("Invalid store database registration.");
        builder.Database = store.DatabaseName;
        return builder.ConnectionString;
    }
}
