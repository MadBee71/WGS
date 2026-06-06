using Microsoft.Data.Sqlite;

namespace WGS.Services;

public class PlayerSession
{
    public long     Id         { get; set; }
    public string   ServerId   { get; set; } = string.Empty;
    public string   PlayerName { get; set; } = string.Empty;
    public string   SteamId    { get; set; } = string.Empty;
    public DateTime JoinTime   { get; set; }
    public DateTime? LeaveTime { get; set; }
    public TimeSpan Duration   => (LeaveTime ?? DateTime.Now) - JoinTime;
    public string   DurationText => $"{(int)Duration.TotalHours:D2}:{Duration.Minutes:D2}:{Duration.Seconds:D2}";
    public bool     IsOnline   => LeaveTime == null;
}

public class PlayerStats
{
    public string   PlayerName   { get; set; } = string.Empty;
    public string   SteamId      { get; set; } = string.Empty;
    public int      SessionCount { get; set; }
    public TimeSpan TotalTime    { get; set; }
    public DateTime LastSeen     { get; set; }
    public string   TotalTimeText => $"{(int)TotalTime.TotalHours}h {TotalTime.Minutes:D2}m";
}

public class PlayerStatsService
{
    private readonly string _connectionString;
    private bool _available = true;

    public PlayerStatsService(ConfigService config)
    {
        var path = System.IO.Path.Combine(config.AppDataPath, "player_stats.db");
        _connectionString = $"Data Source={path}";
        try { EnsureSchema(); }
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
            CREATE TABLE IF NOT EXISTS sessions (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                server_id   TEXT NOT NULL,
                player_name TEXT NOT NULL,
                steam_id    TEXT NOT NULL DEFAULT '',
                join_time   TEXT NOT NULL,
                leave_time  TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_sessions_server   ON sessions(server_id);
            CREATE INDEX IF NOT EXISTS idx_sessions_steamid  ON sessions(steam_id);
            CREATE INDEX IF NOT EXISTS idx_sessions_jointime ON sessions(join_time);

            -- Migraatio: lisaa steam_id jos puuttuu vanhasta skeemasta
            ALTER TABLE sessions ADD COLUMN steam_id TEXT NOT NULL DEFAULT '' ;
            """;
        // ALTER TABLE epäonnistuu jos sarake on jo olemassa — se on ok
        try { cmd.ExecuteNonQuery(); }
        catch
        {
            // Aja ilman ALTER
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS sessions (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    server_id   TEXT NOT NULL,
                    player_name TEXT NOT NULL,
                    steam_id    TEXT NOT NULL DEFAULT '',
                    join_time   TEXT NOT NULL,
                    leave_time  TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_sessions_server   ON sessions(server_id);
                CREATE INDEX IF NOT EXISTS idx_sessions_steamid  ON sessions(steam_id);
                CREATE INDEX IF NOT EXISTS idx_sessions_jointime ON sessions(join_time);
                """;
            cmd.ExecuteNonQuery();
        }
    }

    // ── Kirjaus ───────────────────────────────────────────────────────────────

    public void RecordJoin(string serverId, string playerName, string steamId = "")
    {
        if (!_available) return;
        try
        {
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO sessions (server_id, player_name, steam_id, join_time)
                VALUES ($s, $p, $id, $t)
                """;
            cmd.Parameters.AddWithValue("$s",  serverId);
            cmd.Parameters.AddWithValue("$p",  playerName);
            cmd.Parameters.AddWithValue("$id", steamId);
            cmd.Parameters.AddWithValue("$t",  DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    public void RecordLeave(string serverId, string playerName, string steamId = "")
    {
        if (!_available) return;
        try
        {
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            // Yritä ensin SteamID-matchilla, fallback nimellä
            var where = !string.IsNullOrEmpty(steamId)
                ? "server_id = $s AND steam_id = $id AND leave_time IS NULL"
                : "server_id = $s AND player_name = $p AND leave_time IS NULL";
            cmd.CommandText = $"""
                UPDATE sessions SET leave_time = $t
                WHERE id = (
                    SELECT id FROM sessions
                    WHERE {where}
                    ORDER BY id DESC LIMIT 1
                )
                """;
            cmd.Parameters.AddWithValue("$t",  DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("$s",  serverId);
            cmd.Parameters.AddWithValue("$p",  playerName);
            cmd.Parameters.AddWithValue("$id", steamId);
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    // ── Kyselyt ───────────────────────────────────────────────────────────────

    public List<PlayerSession> GetSessions(string serverId, int limit = 100)
    {
        if (!_available) return [];
        try
        {
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT id, player_name, steam_id, join_time, leave_time
                FROM sessions WHERE server_id=$s
                ORDER BY id DESC LIMIT $l
                """;
            cmd.Parameters.AddWithValue("$s", serverId);
            cmd.Parameters.AddWithValue("$l", limit);
            var result = new List<PlayerSession>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                result.Add(new PlayerSession
                {
                    Id         = r.GetInt64(0),
                    ServerId   = serverId,
                    PlayerName = r.GetString(1),
                    SteamId    = r.IsDBNull(2) ? "" : r.GetString(2),
                    JoinTime   = DateTime.Parse(r.GetString(3)),
                    LeaveTime  = r.IsDBNull(4) ? null : DateTime.Parse(r.GetString(4)),
                });
            return result;
        }
        catch { return []; }
    }

    /// <summary>Palauttaa per-pelaaja tilastot: sessioita, peliaika, viimeksi nähty.</summary>
    public List<PlayerStats> GetPlayerStats(string serverId, int limit = 50)
    {
        if (!_available) return [];
        try
        {
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT
                    player_name,
                    MAX(steam_id),
                    COUNT(*) AS sessions,
                    SUM(CAST(
                        (julianday(COALESCE(leave_time, datetime('now')))
                         - julianday(join_time)) * 86400 AS INTEGER)) AS total_secs,
                    MAX(join_time) AS last_seen
                FROM sessions
                WHERE server_id = $s
                GROUP BY player_name
                ORDER BY total_secs DESC
                LIMIT $l
                """;
            cmd.Parameters.AddWithValue("$s", serverId);
            cmd.Parameters.AddWithValue("$l", limit);
            var result = new List<PlayerStats>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                result.Add(new PlayerStats
                {
                    PlayerName   = r.GetString(0),
                    SteamId      = r.IsDBNull(1) ? "" : r.GetString(1),
                    SessionCount = (int)r.GetInt64(2),
                    TotalTime    = TimeSpan.FromSeconds(r.IsDBNull(3) ? 0 : r.GetDouble(3)),
                    LastSeen     = DateTime.Parse(r.GetString(4)),
                });
            return result;
        }
        catch { return []; }
    }

    // Yhteensopivuus vanhojen kutsujen kanssa
    public Dictionary<string, TimeSpan> GetTotalPlaytime(string serverId)
        => GetPlayerStats(serverId).ToDictionary(s => s.PlayerName, s => s.TotalTime);
}
