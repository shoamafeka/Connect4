using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Connect4_Server.Models
{
    // Registered player. External ID must be unique and in [1..1000].
    public class Player
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [MinLength(2, ErrorMessage = "First name must be at least 2 letters.")]
        [MaxLength(50, ErrorMessage = "First name is too long.")]
        public string FirstName { get; set; } = string.Empty;

        // External ID chosen by the user (1..1000). Uniqueness enforced in DbContext.
        [Range(1, 1000, ErrorMessage = "ID must be an integer between 1 and 1000.")]
        public int PlayerId { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^\d{9,11}$", ErrorMessage = "Phone must contain 9–11 digits.")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Country is required.")]
        public string Country { get; set; } = string.Empty;

        // Navigation: games initiated by this player.
        public ICollection<Game> Games { get; set; } = new List<Game>();
    }
}
