using NEXT.Application.DTOs.Stores;
using Npgsql;
using NEXT.Application.Exceptions;

namespace NEXT.Infrastructure.Stores;

// This catalog always uses the original database. Business repositories use StoreContext instead.
public sealed class StoreCatalog(StoreServerSettings server)
{
    private const string Columns = "\"Id\",\"Name\",\"Slug\",\"DatabaseName\",\"State\",\"OwnerName\",\"OwnerUserName\",\"OwnerEmail\",\"RequestHash\",\"CreatedBy\",\"CreatedAt\",\"Version\"";

    public async Task<IReadOnlyList<StoreRegistration>> ListAsync(CancellationToken ct)
        => await ReadAsync("ORDER BY \"CreatedAt\" DESC,\"Id\"", null, ct);
    public async Task<StoreRegistration?> FindAsync(Guid id, CancellationToken ct)
        => (await ReadAsync("WHERE \"Id\"=@value", id, ct)).SingleOrDefault();
    public async Task<StoreRegistration?> FindSlugAsync(string slug, CancellationToken ct)
        => (await ReadAsync("WHERE \"Slug\"=@value", slug, ct)).SingleOrDefault();

    private async Task<IReadOnlyList<StoreRegistration>> ReadAsync(string suffix, object? value, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(server.RootConnection);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("SELECT " + Columns + " FROM platform.\"Stores\" " + suffix, connection);
        if (value is not null) command.Parameters.AddWithValue("value", value);
        await using var rows = await command.ExecuteReaderAsync(ct);
        var result = new List<StoreRegistration>();
        while (await rows.ReadAsync(ct)) result.Add(new(rows.GetGuid(0), rows.GetString(1), rows.GetString(2), rows.GetString(3),
            (StoreState)rows.GetInt32(4), rows.GetString(5), rows.GetString(6), rows.GetString(7), rows.GetString(8), rows.GetGuid(9),
            new DateTimeOffset(rows.GetDateTime(10)), rows.GetInt32(11)));
        return result;
    }

    public async Task<bool> InsertAsync(StoreRegistration store, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(server.RootConnection);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("""
            INSERT INTO platform."Stores" ("Id","Name","Slug","DatabaseName","State","OwnerName","OwnerUserName","OwnerEmail","RequestHash","CreatedBy","CreatedAt","Version")
            VALUES (@id,@name,@slug,@database,@state,@owner,@username,@email,@hash,@actor,@created,1) ON CONFLICT ("Id") DO NOTHING
            """, connection);
        command.Parameters.AddWithValue("id", store.Id); command.Parameters.AddWithValue("name", store.Name);
        command.Parameters.AddWithValue("slug", store.Slug); command.Parameters.AddWithValue("database", store.DatabaseName);
        command.Parameters.AddWithValue("state", (int)store.State); command.Parameters.AddWithValue("owner", store.OwnerName);
        command.Parameters.AddWithValue("username", store.OwnerUserName); command.Parameters.AddWithValue("email", store.OwnerEmail);
        command.Parameters.AddWithValue("hash", store.RequestHash); command.Parameters.AddWithValue("actor", store.CreatedBy);
        command.Parameters.AddWithValue("created", store.CreatedAt);
        try { return await command.ExecuteNonQueryAsync(ct) == 1; }
        catch (PostgresException error) when (error.SqlState == "23505") { throw new ConflictException("StoreSlugAlreadyUsed"); }
    }

    public async Task<StoreRegistration> ChangeStateAsync(Guid id, StoreState state, int version, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(server.RootConnection);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("""
            UPDATE platform."Stores" SET "State"=@state,"Version"="Version"+1 WHERE "Id"=@id AND "Version"=@version
            """, connection);
        command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("state", (int)state); command.Parameters.AddWithValue("version", version);
        if (await command.ExecuteNonQueryAsync(ct) != 1) throw new ConflictException("StoreChanged");
        return await FindAsync(id, ct) ?? throw new NotFoundException("StoreUnavailable");
    }
}
