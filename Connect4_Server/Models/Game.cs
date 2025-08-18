using System;
using System.Collections.Generic;

namespace Connect4_Server.Models
{
    public enum GameResult
    {
        Unknown = 0,
        HumanWin = 1,
        ServerWin = 2,
        Draw = 3
    }

    // A single Connect4 game played by one player vs the server.
    public class Game
    {
        public int Id { get; set; }

        // UTC start time, set when the game is created on the server.
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        // Total game duration; set when the game ends (zero at start).
        public TimeSpan Duration { get; set; }

        // Compact board serialization (CSV of 42 integers). Example: "0,0,0,...".
        // Stored alongside MoveList to allow quick reconstruction of the board.
        public string? Moves { get; set; }

        public GameResult Result { get; set; } = GameResult.Unknown;

        // FK to Player.Id (internal DB PK). Do NOT confuse with Player.PlayerId (external 1..1000).
        public int PlayerId { get; set; }
        public Player Player { get; set; } = null!;

        // Navigation to the list of persisted moves for this game.
        public ICollection<Move> MoveList { get; set; } = new List<Move>();
    }
}
