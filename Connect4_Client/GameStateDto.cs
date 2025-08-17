namespace Connect4_Client
{
    public class GameStateDto
    {
        public int GameId { get; set; }
        public int[,] Board { get; set; }
        public int CurrentPlayer { get; set; }
        public string Status { get; set; }
    }
}
