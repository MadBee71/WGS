using System.IO;
using Microsoft.Data.Sqlite;

namespace WGS.Services;

public class WorkshopMod
{
    public string   ServerId    { get; set; } = string.Empty;
    public ulong    ModId       { get; set; }
    public string   ModName     { get; set; } = string.Empty;
    public bool     IsEnabled   { get; set; } = true;
    public DateTime LastUpdated { get; set; } = DateTime.MinValue;
}

/// <summary>Persists per-server mod list in SQLite (server_mods table).</summary>
public class WorkshopDbService
{
    private readonly string _connectionString;
    private bool _available = true;

    public WorkshopDbService(ConfigService config)
    {
        var path = Path.Combine(config.AppDataPath, "workshop.db");
        _connectionString = $"Data Source={path}";
        TryEnsureSchema();
    }

    private void TryEnsureSchema()
    {
        try { EnsureSchema(); _available = true; }
        catch { _available = false; }
    }

    private SqliteConnection OpenDb()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void EnsureSchema()
    {
        using var db  = OpenDb();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS server_mods (
                server_id    TEXT NOT NULL,
                mod_id       TEXT NOT NULL,
                mod_name     TEXT NOT NULL DEFAULT '',
                is_enabled   INTEGER NOT NULL DEFAULT 1,
                last_updated TEXT NOT NULL DEFAULT '',
                PRIMARY KEY (server_id, mod_id)
            );
            CREATE INDEX IF NOT EXISTS idx_server_mods_server ON server_mods(server_id);
            """;
        cmd.ExecuteNonQuery();
    }

    public List<WorkshopMod> GetModsForServer(string serverId)
    {
        if (!_available) { TryEnsureSchema(); }
        if (!_available) return [];
        try
        {
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT mod_id, mod_name, is_enabled, last_updated FROM server_mods WHERE server_id=$s ORDER BY mod_name";
            cmd.Parameters.AddWithValue("$s", serverId);
            using var reader = cmd.ExecuteReader();
            var result = new List<WorkshopMod>();
            while (reader.Read())
            {
                result.Add(new WorkshopMod
                {
                    ServerId    = serverId,
                    ModId       = ulong.TryParse(reader.GetString(0), out var id) ? id : 0,
                    ModName     = reader.GetString(1),
                    IsEnabled   = reader.GetInt32(2) != 0,
                    LastUpdated = DateTime.TryParse(reader.GetString(3), out var dt) ? dt : DateTime.MinValue,
                });
            }
            return result;
        }
        catch { return []; }
    }

    public void UpsertMod(WorkshopMod mod)
    {
        if (!_available) return;
        try
        {
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO server_mods (server_id, mod_id, mod_name, is_enabled, last_updated)
                VALUES ($s, $id, $name, $enabled, $updated)
                ON CONFLICT(server_id, mod_id) DO UPDATE SET
                    mod_name     = excluded.mod_name,
                    is_enabled   = excluded.is_enabled,
                    last_updated = excluded.last_updated
                """;
            cmd.Parameters.AddWithValue("$s",       mod.ServerId);
            cmd.Parameters.AddWithValue("$id",      mod.ModId.ToString());
            cmd.Parameters.AddWithValue("$name",    mod.ModName);
            cmd.Parameters.AddWithValue("$enabled", mod.IsEnabled ? 1 : 0);
            cmd.Parameters.AddWithValue("$updated", mod.LastUpdated.ToString("o"));
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    public void SetEnabled(string serverId, ulong modId, bool enabled)
    {
        if (!_available) return;
        try
        {
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "UPDATE server_mods SET is_enabled=$e WHERE server_id=$s AND mod_id=$id";
            cmd.Parameters.AddWithValue("$e",  enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("$s",  serverId);
            cmd.Parameters.AddWithValue("$id", modId.ToString());
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    public void DeleteMod(string serverId, ulong modId)
    {
        if (!_available) return;
        try
        {
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "DELETE FROM server_mods WHERE server_id=$s AND mod_id=$id";
            cmd.Parameters.AddWithValue("$s",  serverId);
            cmd.Parameters.AddWithValue("$id", modId.ToString());
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    public void DeleteAllForServer(string serverId)
    {
        if (!_available) return;
        try
        {
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "DELETE FROM server_mods WHERE server_id=$s";
            cmd.Parameters.AddWithValue("$s", serverId);
            cmd.ExecuteNonQuery();
        }
        catch { }
    }
}
