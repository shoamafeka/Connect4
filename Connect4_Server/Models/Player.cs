using System;
using System.ComponentModel.DataAnnotations;

namespace Connect4_Server.Models
{
    public class Player
    {
        public int Id { get; set; }

        [Required]
        [MinLength(2)]
        public string FirstName { get; set; }

        [Range(1, 1000)]
        public int PlayerId { get; set; }

        //[Phone]
        public string Phone { get; set; }

        public string Country { get; set; }

        public ICollection<Game> Games { get; set; } = new List<Game>();
    }
}
