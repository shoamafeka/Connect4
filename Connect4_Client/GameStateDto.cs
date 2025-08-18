namespace Connect4_Client
{
    // Client-side game state model used by the UI/logic.
    // IMPORTANT: The server API returns the board as a JAGGED array (int[][]).
    // This client uses a RECTANGULAR matrix (int[,]) for internal logic.
    // Make sure the API layer converts int[][] -> int[,] before assigning to Board.
    public class GameStateDto
    {
        public int GameId { get; set; }
        public int[,] Board { get; set; } = new int[6, 7]; // 6x7 Connect4 board (0=empty, 1=human, 2=server)
        public int CurrentPlayer { get; set; }             // 1=human turn, 2=server turn (or per your convention)
        public string Status { get; set; } = string.Empty; // "ongoing" / "player_won" / "server_won" / "draw"
    }
}
