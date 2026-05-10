using Microsoft.Data.Sqlite;

namespace WGS.Services;

public class PlayerSession
{
    public long     Id        { get; set; }
    public string   ServerId  { get; set; } = string.Empty;
    public string   PlayerName{ get; set; } = string.Empty;
    public DateTime JoinTime  { get; set; }
    public DateTime? LeaveTime{ get; set; }
    public TimeSpan Duration  => (LeaveTime ?? DateTime.Now) - JoinTime;
    public string   DurationText => $"{(int)Duration.TotalHours:D2}:{Duration.Minutes:D2}:{Duration.Seconds:D2}";
}

public class PlayerStatsService : IDisposable
{
    private readonly SqliteConnection _db;

    public PlayerStatsService(ConfigService config)
    {
        var path = System.IO.Path.Combine(config.AppDataPath, "player_stats.db");
        _db = new SqliteConnection($"Data Source={path}");
        _db.Open();
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS sessions (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                server_id   TEXT NOT NULL,
                player_name TEXT NOT NULL,
                join_time   TEXT NOT NULL,
                leave_time  TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_sessions_server ON sessions(server_id);
            """;
        cmd.ExecuteNonQuery();
    }

    public void RecordJoin(string serverId, string playerName)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "INSERT INTO sessions (server_id, player_name, join_time) VALUES ($s, $p, $t)";
        cmd.Parameters.AddWithValue("$s", serverId);
        cmd.Parameters.AddWithValue("$p", playerName);
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public void RecordLeave(string serverId, string playerName)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            UPDATE sessions SET leave_time = $t
            WHERE server_id = $s AND player_name = $p AND leave_time IS NULL
            ORDER BY id DESC LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$s", serverId);
        cmd.Parameters.AddWithValue("$p", playerName);
        cmd.ExecuteNonQuery();
    }

    public List<PlayerSession> GetSessions(string serverId, int limit = 100)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT id, player_name, join_time, leave_time FROM sessions WHERE server_id=$s ORDER BY id DESC LIMIT $l";
        cmd.Parameters.AddWithValue("$s", serverId);
        cmd.Parameters.AddWithValue("$l", limit);
        var result = new List<PlayerSession>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            result.Add(new PlayerSession
            {
                Id         = r.GetInt64(0),
                ServerId   = serverId,
                PlayerName = r.GetString(1),
                JoinTime   = DateTime.Parse(r.GetString(2)),
                LeaveTime  = r.IsDBNull(3) ? null : DateTime.Parse(r.GetString(3)),
            });
        }
        return result;
    }

    public Dictionary<string, TimeSpan> GetTotalPlaytime(string serverId)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            SELECT player_name,
                   SUM(CAST((julianday(COALESCE(leave_time, datetime('now'))) - julianday(join_time)) * 86400 AS INTEGER))
            FROM sessions WHERE server_id=$s GROUP BY player_name
            """;
        cmd.Parameters.AddWithValue("$s", serverId);
        var result = new Dictionary<string, TimeSpan>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            result[r.GetString(0)] = TimeSpan.FromSeconds(r.GetDouble(1));
        return result;
    }

    public void Dispose() => _db.Dispose();
}
