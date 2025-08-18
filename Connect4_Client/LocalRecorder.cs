using System;
using System.Collections.Generic;
using System.Data.SqlClient; // ADO.NET with SQL Server LocalDB (allowed by the assignment)
using System.IO;

namespace Connect4_Client
{
    // -----------------------------
    // DTOs for the local recording
    // -----------------------------

    // High-level game info used for listing and picking a replay.
    public class LocalGameInfo
    {
        public int LocalGameId { get; set; }
        public int ServerGameId { get; set; }
        public int PlayerExternalId { get; set; }
        public string PlayerName { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public int? DurationSeconds { get; set; }
        public byte Result { get; set; } // 0=Unknown, 1=HumanWin, 2=ServerWin, 3=Draw
    }

    // One recorded move within a local game.
    public class MoveRecord
    {
        public int TurnIndex { get; set; }   // 0..n (in-order)
        public int Column { get; set; }      // 0..6
        public byte PlayerType { get; set; } // 1=Human, 2=Server
    }

    // --------------------------------------------------------------------
    // LocalRecorder: persists games and moves to a SQL Server LocalDB file.
    // Schema:
    //   dbo.LocalGames(
    //     LocalGameId INT IDENTITY PK,
    //     ServerGameId INT UNIQUE,
    //     PlayerExternalId INT,
    //     PlayerName NVARCHAR(50) NULL,
    //     StartedAtUtc DATETIME NOT NULL,
    //     DurationSeconds INT NULL,
    //     Result TINYINT NOT NULL DEFAULT 0)
    //
    //   dbo.LocalMoves(
    //     Id INT IDENTITY PK,
    //     LocalGameId INT NOT NULL FK -> LocalGames(LocalGameId) ON DELETE CASCADE,
    //     TurnIndex INT NOT NULL, -- unique per game
    //     [Column] TINYINT NOT NULL, -- 0..6
    //     PlayerType TINYINT NOT NULL, -- 1=Human,2=Server
    //     CONSTRAINT UQ_LocalMoves UNIQUE(LocalGameId, TurnIndex))
    //
    // Purpose:
    // - Satisfies the requirement to locally "record" games and support replay.
    // - Keeps minimal, self-contained ADO.NET logic (no ORM on client).
    // --------------------------------------------------------------------
    public class LocalRecorder
    {
        private const string DbName = "Connect4Client";

        private readonly string _mdfPath;
        private readonly string _ldfPath;

        // Connection to master for DB creation.
        private const string MasterConn = @"Data Source=(LocalDB)\MSSQLLocalDB;Integrated Security=True;";

        // Connection to the file-attached database.
        private string DbConn => @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=" + _mdfPath + @";Integrated Security=True;";

        // Default folder: %LocalAppData%\Connect4\Client
        public static string DefaultFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Connect4", "Client");

        public static string DefaultMdfPath => Path.Combine(DefaultFolder, "Connect4Client.mdf");

        public LocalRecorder()
        {
            // Ensure directory and database exist; if missing, create and initialize schema.
            Directory.CreateDirectory(DefaultFolder);
            _mdfPath = DefaultMdfPath;
            _ldfPath = Path.Combine(DefaultFolder, "Connect4Client_log.ldf");
            EnsureDatabaseAndSchema();
        }

        // Ensures the .mdf exists and that schema (tables) are created.
        private void EnsureDatabaseAndSchema()
        {
            // Create database file if missing
            if (!File.Exists(_mdfPath))
            {
                using (var conn = new SqlConnection(MasterConn))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        // Escape single quotes in file paths
                        string mdfEsc = _mdfPath.Replace("'", "''");
                        string ldfEsc = _ldfPath.Replace("'", "''");

                        // Create a new LocalDB database backed by specific file paths
                        cmd.CommandText = string.Format(@"
IF DB_ID(N'{0}') IS NULL
BEGIN
    CREATE DATABASE [{0}]
    ON (NAME = N'{0}', FILENAME = N'{1}')
    LOG ON (NAME = N'{0}_log', FILENAME = N'{2}');
END", DbName, mdfEsc, ldfEsc);

                        cmd.ExecuteNonQuery();
                    }
                }
            }

            // Attach (via AttachDbFilename) and ensure tables exist
            using (var conn2 = new SqlConnection(DbConn))
            {
                conn2.Open();
                using (var cmd2 = conn2.CreateCommand())
                {
                    cmd2.CommandText = @"
IF OBJECT_ID('dbo.LocalGames','U') IS NULL
BEGIN
  CREATE TABLE dbo.LocalGames(
    LocalGameId INT IDENTITY(1,1) PRIMARY KEY,
    ServerGameId INT NOT NULL UNIQUE,
    PlayerExternalId INT NOT NULL,
    PlayerName NVARCHAR(50) NULL,
    StartedAtUtc DATETIME NOT NULL,
    DurationSeconds INT NULL,
    Result TINYINT NOT NULL DEFAULT 0
  );
END
IF OBJECT_ID('dbo.LocalMoves','U') IS NULL
BEGIN
  CREATE TABLE dbo.LocalMoves(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    LocalGameId INT NOT NULL,
    TurnIndex INT NOT NULL,
    [Column] TINYINT NOT NULL,
    PlayerType TINYINT NOT NULL,
    CONSTRAINT UQ_LocalMoves UNIQUE(LocalGameId, TurnIndex),
    CONSTRAINT FK_LocalMoves_LocalGames FOREIGN KEY(LocalGameId)
      REFERENCES dbo.LocalGames(LocalGameId) ON DELETE CASCADE
  );
END";
                    cmd2.ExecuteNonQuery();
                }
            }
        }

        // Creates a local game row for a given server-side GameId, or returns existing LocalGameId.
        public int EnsureLocalGame(int serverGameId, int playerExternalId, string playerName, DateTime startedAtUtc)
        {
            using (var conn = new SqlConnection(DbConn))
            {
                conn.Open();

                // Check if we already recorded this server game id
                using (var check = new SqlCommand("SELECT LocalGameId FROM dbo.LocalGames WHERE ServerGameId=@g", conn))
                {
                    check.Parameters.AddWithValue("@g", serverGameId);
                    var v = check.ExecuteScalar();
                    if (v != null && v != DBNull.Value)
                        return Convert.ToInt32(v);
                }

                // Insert a new local game row
                using (var ins = new SqlCommand(@"
INSERT INTO dbo.LocalGames(ServerGameId,PlayerExternalId,PlayerName,StartedAtUtc,Result)
VALUES(@g,@p,@n,@s,0);
SELECT SCOPE_IDENTITY();", conn))
                {
                    ins.Parameters.AddWithValue("@g", serverGameId);
                    ins.Parameters.AddWithValue("@p", playerExternalId);
                    ins.Parameters.AddWithValue("@n", playerName == null ? (object)DBNull.Value : (object)playerName);
                    ins.Parameters.AddWithValue("@s", startedAtUtc);
                    return Convert.ToInt32((decimal)ins.ExecuteScalar());
                }
            }
        }

        // Appends one move to the local recording (TurnIndex is unique per LocalGameId).
        public void AddMove(int localGameId, int turnIndex, int column, byte playerType)
        {
            using (var conn = new SqlConnection(DbConn))
            {
                conn.Open();
                using (var cmd = new SqlCommand(@"
INSERT INTO dbo.LocalMoves(LocalGameId,TurnIndex,[Column],PlayerType)
VALUES(@g,@t,@c,@p);", conn))
                {
                    cmd.Parameters.AddWithValue("@g", localGameId);
                    cmd.Parameters.AddWithValue("@t", turnIndex);
                    cmd.Parameters.AddWithValue("@c", column);
                    cmd.Parameters.AddWithValue("@p", playerType);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // Marks a game as finished with final result and total duration.
        public void FinishGame(int localGameId, byte result, int durationSeconds)
        {
            using (var conn = new SqlConnection(DbConn))
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                    "UPDATE dbo.LocalGames SET Result=@r, DurationSeconds=@d WHERE LocalGameId=@g;", conn))
                {
                    cmd.Parameters.AddWithValue("@g", localGameId);
                    cmd.Parameters.AddWithValue("@r", result);
                    cmd.Parameters.AddWithValue("@d", durationSeconds);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // Lists all locally recorded games for the given external player id (most recent first).
        public List<LocalGameInfo> ListGamesForPlayer(int playerExternalId)
        {
            var res = new List<LocalGameInfo>();
            using (var conn = new SqlConnection(DbConn))
            {
                conn.Open();
                using (var cmd = new SqlCommand(@"
SELECT LocalGameId, ServerGameId, PlayerExternalId, ISNULL(PlayerName,''), StartedAtUtc, DurationSeconds, Result
FROM dbo.LocalGames
WHERE PlayerExternalId=@p
ORDER BY StartedAtUtc DESC;", conn))
                {
                    cmd.Parameters.AddWithValue("@p", playerExternalId);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            var info = new LocalGameInfo
                            {
                                LocalGameId = rd.GetInt32(0),
                                ServerGameId = rd.GetInt32(1),
                                PlayerExternalId = rd.GetInt32(2),
                                PlayerName = rd.GetString(3),
                                StartedAtUtc = rd.GetDateTime(4),
                                DurationSeconds = rd.IsDBNull(5) ? (int?)null : rd.GetInt32(5),
                                Result = rd.GetByte(6)
                            };
                            res.Add(info);
                        }
                    }
                }
            }
            return res;
        }

        // Loads all moves for a recorded local game (ordered by TurnIndex).
        public List<MoveRecord> LoadMoves(int localGameId)
        {
            var res = new List<MoveRecord>();
            using (var conn = new SqlConnection(DbConn))
            {
                conn.Open();
                using (var cmd = new SqlCommand(@"
SELECT TurnIndex, [Column], PlayerType
FROM dbo.LocalMoves
WHERE LocalGameId=@g
ORDER BY TurnIndex;", conn))
                {
                    cmd.Parameters.AddWithValue("@g", localGameId);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            var mv = new MoveRecord
                            {
                                TurnIndex = rd.GetInt32(0),         // INT
                                Column = rd.GetByte(1),             // TINYINT -> byte (widened to int)
                                PlayerType = rd.GetByte(2)          // TINYINT -> byte
                            };
                            res.Add(mv);
                        }
                    }
                }
            }
            return res;
        }

        // Resolves a LocalGameId given a server-side GameId (used when launching a replay from the website).
        public int? FindLocalGameIdByServerGameId(int serverGameId)
        {
            using (var conn = new SqlConnection(DbConn))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT LocalGameId FROM dbo.LocalGames WHERE ServerGameId=@g;", conn))
                {
                    cmd.Parameters.AddWithValue("@g", serverGameId);
                    var v = cmd.ExecuteScalar();
                    if (v == null || v == DBNull.Value) return null;
                    return Convert.ToInt32(v);
                }
            }
        }
    }
}
