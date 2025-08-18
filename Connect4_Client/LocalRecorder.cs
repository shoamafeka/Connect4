using System;
using System.Collections.Generic;
using System.Data.SqlClient; // If you're on .NET 6/7/8 WinForms, switch to Microsoft.Data.SqlClient and update usings.
using System.IO;

namespace Connect4_Client
{
    // DTOs (C# 7.3-friendly)
    public class LocalGameInfo
    {
        public int LocalGameId { get; set; }
        public int ServerGameId { get; set; }
        public int PlayerExternalId { get; set; }
        public string PlayerName { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public int? DurationSeconds { get; set; }
        public byte Result { get; set; } // 0=Unknown,1=HumanWin,2=ServerWin,3=Draw
    }

    public class MoveRecord
    {
        public int TurnIndex { get; set; }  // 0..n
        public int Column { get; set; }     // 0..6
        public byte PlayerType { get; set; } // 1=Human, 2=Server
    }

    /// <summary>
    /// Local SQL Server LocalDB recorder for Connect4 replays.
    /// Tables:
    ///   LocalGames(LocalGameId PK, ServerGameId UNIQUE, PlayerExternalId, PlayerName, StartedAtUtc, DurationSeconds NULL, Result tinyint)
    ///   LocalMoves(Id PK, LocalGameId FK, TurnIndex UNIQUE per game, Column, PlayerType)
    /// </summary>
    public class LocalRecorder
    {
        private const string DbName = "Connect4Client";

        private readonly string _mdfPath;
        private readonly string _ldfPath;

        private const string MasterConn = @"Data Source=(LocalDB)\MSSQLLocalDB;Integrated Security=True;";
        private string DbConn
        {
            get
            {
                return @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=" + _mdfPath + @";Integrated Security=True;";
            }
        }

        public static string DefaultFolder
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Connect4", "Client");
            }
        }

        public static string DefaultMdfPath
        {
            get { return Path.Combine(DefaultFolder, "Connect4Client.mdf"); }
        }

        public LocalRecorder()
        {
            Directory.CreateDirectory(DefaultFolder);
            _mdfPath = DefaultMdfPath;
            _ldfPath = Path.Combine(DefaultFolder, "Connect4Client_log.ldf");
            EnsureDatabaseAndSchema();
        }

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

                        // Use a direct CREATE DATABASE (no EXEC, no outer variables)
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

            // Attach (auto-attach via AttachDbFilename) and ensure tables
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


        /// <summary>Creates or returns an existing LocalGameId for the given serverGameId.</summary>
        public int EnsureLocalGame(int serverGameId, int playerExternalId, string playerName, DateTime startedAtUtc)
        {
            using (var conn = new SqlConnection(DbConn))
            {
                conn.Open();

                using (var check = new SqlCommand("SELECT LocalGameId FROM dbo.LocalGames WHERE ServerGameId=@g", conn))
                {
                    check.Parameters.AddWithValue("@g", serverGameId);
                    var v = check.ExecuteScalar();
                    if (v != null && v != DBNull.Value)
                        return Convert.ToInt32(v);
                }

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

        /// <summary>Append a move (turnIndex increases monotonically 0..n).</summary>
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

        /// <summary>Mark game as finished.</summary>
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

        /// <summary>List locally recorded games for a specific player.</summary>
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
                            var info = new LocalGameInfo();
                            info.LocalGameId = rd.GetInt32(0);
                            info.ServerGameId = rd.GetInt32(1);
                            info.PlayerExternalId = rd.GetInt32(2);
                            info.PlayerName = rd.GetString(3);
                            info.StartedAtUtc = rd.GetDateTime(4);
                            info.DurationSeconds = rd.IsDBNull(5) ? (int?)null : rd.GetInt32(5);
                            info.Result = rd.GetByte(6);
                            res.Add(info);
                        }
                    }
                }
            }
            return res;
        }

        /// <summary>Load ordered moves for a recorded game.</summary>
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
                            var mv = new MoveRecord();
                            mv.TurnIndex = rd.GetInt32(0);          // INT
                            mv.Column = rd.GetByte(1);           // TINYINT -> byte -> (widen) int
                            mv.PlayerType = rd.GetByte(2);           // TINYINT -> byte
                            res.Add(mv);
                        }
                    }
                }
            }
            return res;
        }


        /// <summary>Find local game id by the server's GameId (for launching replay from the website).</summary>
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
