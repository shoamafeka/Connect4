using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Connect4_Server.Models;

/// <summary>
/// Represents a single move in a Connect4 game.
/// </summary>
public class Move
{
    public int Id { get; set; }

    /// <summary>Order of the move in the game (starting from 1).</summary>
    [Range(1, int.MaxValue)]
    public int MoveNumber { get; set; }

    /// <summary>The column chosen by the player or server.</summary>
    [Range(0, 6, ErrorMessage = "Column must be between 0 and 6.")]
    public int Column { get; set; }

    /// <summary>True if made by the human player, false if made by server.</summary>
    public bool IsPlayerMove { get; set; }

    // Foreign key to Game
    public int GameId { get; set; }
    public Game Game { get; set; } = null!;
}
