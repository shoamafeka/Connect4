using System;

namespace Connect4_Server.Models
{
    public class Game
    {

        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public string Moves { get; set; } // e.g. CSV string: "3,2,4,5,..."

        public int PlayerId { get; set; }
        public Player Player { get; set; }
    }
}
