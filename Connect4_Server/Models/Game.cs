namespace Connect4_Server.Models
{
    public enum GameResult
    {
        Unknown = 0,
        HumanWin = 1,
        ServerWin = 2,
        Draw = 3
    }

    /// <summary>
    /// A single Connect4 game played by one player vs the server.
    /// </summary>
    public class Game
    {
        public int Id { get; set; }

        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Optional compact serialization of moves, e.g., "3,2,4,5".
        /// </summary>
        public string? Moves { get; set; }

        public GameResult Result { get; set; } = GameResult.Unknown;

        public int PlayerId { get; set; }
        public Player Player { get; set; } = null!;

        public ICollection<Move> MoveList { get; set; } = new List<Move>();
    }
}
