using System.ComponentModel.DataAnnotations;

namespace Connect4_Server.Models
{
    // Represents a single move in a Connect4 game.
    public class Move
    {
        public int Id { get; set; }

        // Order of the move in the game (1-based).
        [Range(1, int.MaxValue)]
        public int MoveNumber { get; set; }

        // The column chosen by the player or server (0..6).
        [Range(0, 6, ErrorMessage = "Column must be between 0 and 6.")]
        public int Column { get; set; }

        // True if made by the human player; false if made by the server.
        public bool IsPlayerMove { get; set; }

        // Foreign key to Game.Id
        public int GameId { get; set; }
        public Game Game { get; set; } = null!;
    }
}
