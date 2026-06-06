using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace WGS.Services;

public enum UserRole { Admin, Viewer }

public class WgsUser
{
    public int       Id         { get; set; }
    public string    Username   { get; set; } = string.Empty;
    public UserRole  Role       { get; set; } = UserRole.Viewer;
    public string    Token      { get; set; } = string.Empty;
    public bool      IsEnabled  { get; set; } = true;
    public DateTime? LastLogin  { get; set; }

    public string RoleLabel    => Role == UserRole.Admin ? "Admin" : "Viewer";
    public string StatusLabel  => IsEnabled ? "Aktiivinen" : "Poistettu käytöstä";
    public string LastLoginText => LastLogin.HasValue
        ? LastLogin.Value.ToString("dd.MM.yyyy HH:mm")
        : "Ei koskaan";
}

public class AuditEntry
{
    public int      Id         { get; set; }
    public string   Username   { get; set; } = string.Empty;
    public string   Action     { get; set; } = string.Empty;
    public string   Detail     { get; set; } = string.Empty;
    public DateTime Timestamp  { get; set; }
    public string   TimestampText => Timestamp.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");
}

public class UserService
{
    private readonly string _connectionString;
    private bool _available = true;

    public UserService(ConfigService config)
    {
        var path = Path.Combine(config.AppDataPath, "users.db");
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
            CREATE TABLE IF NOT EXISTS users (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                username      TEXT    NOT NULL UNIQUE,
                password_hash TEXT    NOT NULL,
                role          TEXT    NOT NULL DEFAULT 'Viewer',
                token         TEXT    NOT NULL,
                is_enabled    INTEGER NOT NULL DEFAULT 1,
                last_login    TEXT
            );
            CREATE TABLE IF NOT EXISTS audit_log (
                id        INTEGER PRIMARY KEY AUTOINCREMENT,
                username  TEXT NOT NULL,
                action    TEXT NOT NULL,
                detail    TEXT NOT NULL DEFAULT '',
                ts        TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_audit_ts ON audit_log(ts);
            """;
        cmd.ExecuteNonQuery();

        // Migraatio: lisaa last_login jos puuttuu
        try
        {
            using var alt = db.CreateCommand();
            alt.CommandText = "ALTER TABLE users ADD COLUMN last_login TEXT";
            alt.ExecuteNonQuery();
        }
        catch { }

        using var count = db.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM users";
        var n = (long)(count.ExecuteScalar() ?? 0L);
        if (n == 0)
            CreateUser("admin", "admin", UserRole.Admin);
    }

    public List<WgsUser> GetAll()
    {
        if (!_available) return [];
        try
        {
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT id, username, role, token, is_enabled, last_login FROM users ORDER BY id";
            using var r = cmd.ExecuteReader();
            var list = new List<WgsUser>();
            while (r.Read())
                list.Add(new WgsUser
                {
                    Id        = r.GetInt32(0),
                    Username  = r.GetString(1),
                    Role      = Enum.TryParse<UserRole>(r.GetString(2), out var role) ? role : UserRole.Viewer,
                    Token     = r.GetString(3),
                    IsEnabled = r.GetInt32(4) != 0,
                    LastLogin = r.IsDBNull(5) ? null : DateTime.Parse(r.GetString(5)),
                });
            return list;
        }
        catch { return []; }
    }

    public bool ValidateToken(string token, out WgsUser? user)
    {
        user = null;
        if (!_available || string.IsNullOrEmpty(token)) return false;
        try
        {
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT id, username, role, token, is_enabled, last_login FROM users WHERE token=$t AND is_enabled=1";
            cmd.Parameters.AddWithValue("$t", token);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return false;
            user = new WgsUser
            {
                Id        = r.GetInt32(0),
                Username  = r.GetString(1),
                Role      = Enum.TryParse<UserRole>(r.GetString(2), out var role) ? role : UserRole.Viewer,
                Token     = r.GetString(3),
                IsEnabled = true,
                LastLogin = r.IsDBNull(5) ? null : DateTime.Parse(r.GetString(5)),
            };
            RecordLogin(user.Id, user.Username, "token");
            return true;
        }
        catch { return false; }
    }

    public bool ValidatePassword(string username, string password, out WgsUser? user)
    {
        user = null;
        if (!_available) return false;
        try
        {
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT id, username, role, token, is_enabled, password_hash, last_login FROM users WHERE username=$u AND is_enabled=1";
            cmd.Parameters.AddWithValue("$u", username);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) { WriteAudit(username, "login_fail", "bad password"); return false; }
            var hash = r.GetString(5);
            if (!VerifyPassword(password, hash)) { WriteAudit(username, "login_fail", "bad password"); return false; }
            user = new WgsUser
            {
                Id        = r.GetInt32(0),
                Username  = r.GetString(1),
                Role      = Enum.TryParse<UserRole>(r.GetString(2), out var role) ? role : UserRole.Viewer,
                Token     = r.GetString(3),
                IsEnabled = true,
                LastLogin = r.IsDBNull(6) ? null : DateTime.Parse(r.GetString(6)),
            };
            RecordLogin(user.Id, user.Username, "password");
            return true;
        }
        catch { return false; }
    }

    private void RecordLogin(int userId, string username, string method)
    {
        try
        {
            using var db = OpenDb();
            using var upd = db.CreateCommand();
            upd.CommandText = "UPDATE users SET last_login=$t WHERE id=$id";
            upd.Parameters.AddWithValue("$t",  DateTime.UtcNow.ToString("o"));
            upd.Parameters.AddWithValue("$id", userId);
            upd.ExecuteNonQuery();
            WriteAudit(username, "login", method, db);
        }
        catch { }
    }

    public void CreateUser(string username, string password, UserRole role)
    {
        if (!_available) return;
        try
        {
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "INSERT INTO users (username, password_hash, role, token) VALUES ($u, $h, $r, $t)";
            cmd.Parameters.AddWithValue("$u", username);
            cmd.Parameters.AddWithValue("$h", HashPassword(password));
            cmd.Parameters.AddWithValue("$r", role.ToString());
            cmd.Parameters.AddWithValue("$t", Guid.NewGuid().ToString("N"));
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    public void ChangePassword(int userId, string newPassword, string changedBy = "")
    {
        if (!_available) return;
        try
        {
            var username = GetUsername(userId);
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "UPDATE users SET password_hash=$h WHERE id=$id";
            cmd.Parameters.AddWithValue("$h",  HashPassword(newPassword));
            cmd.Parameters.AddWithValue("$id", userId);
            cmd.ExecuteNonQuery();
            WriteAudit(changedBy.Length > 0 ? changedBy : username, "change_password", $"user={username}", db);
        }
        catch { }
    }

    public void ChangeRole(int userId, UserRole newRole, string changedBy = "")
    {
        if (!_available) return;
        try
        {
            var username = GetUsername(userId);
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "UPDATE users SET role=$r WHERE id=$id";
            cmd.Parameters.AddWithValue("$r",  newRole.ToString());
            cmd.Parameters.AddWithValue("$id", userId);
            cmd.ExecuteNonQuery();
            WriteAudit(changedBy.Length > 0 ? changedBy : username, "change_role",
                $"user={username} new_role={newRole}", db);
        }
        catch { }
    }

    public void RegenerateToken(int userId, string changedBy = "")
    {
        if (!_available) return;
        try
        {
            var username = GetUsername(userId);
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "UPDATE users SET token=$t WHERE id=$id";
            cmd.Parameters.AddWithValue("$t",  Guid.NewGuid().ToString("N"));
            cmd.Parameters.AddWithValue("$id", userId);
            cmd.ExecuteNonQuery();
            WriteAudit(changedBy.Length > 0 ? changedBy : username, "regen_token", $"user={username}", db);
        }
        catch { }
    }

    public void SetEnabled(int userId, bool enabled, string changedBy = "")
    {
        if (!_available) return;
        try
        {
            var username = GetUsername(userId);
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "UPDATE users SET is_enabled=$e WHERE id=$id";
            cmd.Parameters.AddWithValue("$e",  enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", userId);
            cmd.ExecuteNonQuery();
            WriteAudit(changedBy.Length > 0 ? changedBy : "admin",
                enabled ? "enable_user" : "disable_user", $"user={username}", db);
        }
        catch { }
    }

    public void DeleteUser(int userId, string deletedBy = "")
    {
        if (!_available) return;
        try
        {
            var username = GetUsername(userId);
            using var db  = OpenDb();
            WriteAudit(deletedBy.Length > 0 ? deletedBy : "admin", "delete_user", $"user={username}", db);
            using var cmd = db.CreateCommand();
            cmd.CommandText = "DELETE FROM users WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", userId);
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    // ── Audit log ─────────────────────────────────────────────────────────────

    public List<AuditEntry> GetAuditLog(int limit = 100)
    {
        if (!_available) return [];
        try
        {
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT id, username, action, detail, ts FROM audit_log ORDER BY id DESC LIMIT $l";
            cmd.Parameters.AddWithValue("$l", limit);
            var list = new List<AuditEntry>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new AuditEntry
                {
                    Id        = r.GetInt32(0),
                    Username  = r.GetString(1),
                    Action    = r.GetString(2),
                    Detail    = r.GetString(3),
                    Timestamp = DateTime.Parse(r.GetString(4)),
                });
            return list;
        }
        catch { return []; }
    }

    // Julkinen overload ilman olemassa olevaa yhteyttä
    public void WriteAudit(string username, string action, string detail = "")
    {
        if (!_available) return;
        try
        {
            using var db = OpenDb();
            WriteAudit(username, action, detail, db);
        }
        catch { }
    }

    private static void WriteAudit(string username, string action, string detail, SqliteConnection db)
    {
        try
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "INSERT INTO audit_log (username, action, detail, ts) VALUES ($u,$a,$d,$t)";
            cmd.Parameters.AddWithValue("$u", username);
            cmd.Parameters.AddWithValue("$a", action);
            cmd.Parameters.AddWithValue("$d", detail);
            cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    private string GetUsername(int userId)
    {
        try
        {
            using var db  = OpenDb();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT username FROM users WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", userId);
            return cmd.ExecuteScalar()?.ToString() ?? userId.ToString();
        }
        catch { return userId.ToString(); }
    }

    // ── Password hashing (PBKDF2-SHA256) ──────────────────────────────────────

    private static string HashPassword(string password)
    {
        var salt  = RandomNumberGenerator.GetBytes(16);
        var hash  = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt,
            iterations: 100_000, HashAlgorithmName.SHA256, outputLength: 32);
        return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            var parts = storedHash.Split(':');
            if (parts.Length != 2) return false;
            var salt     = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            var actual   = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), salt,
                iterations: 100_000, HashAlgorithmName.SHA256, outputLength: 32);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch { return false; }
    }
}
