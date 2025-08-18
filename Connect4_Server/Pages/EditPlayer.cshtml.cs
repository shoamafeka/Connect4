using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Connect4_Server.Models;
using Connect4_Server.Data;
using System.Linq;

namespace Connect4_Server.Pages
{
    // EditPlayer page model:
    // - Loads a player by internal DB Id.
    // - Allows changing FirstName/Phone/Country.
    // - External PlayerId can be changed only if the player has no games yet.
    public class EditPlayerModel : PageModel
    {
        private readonly AppDbContext _context;

        public EditPlayerModel(AppDbContext context)
        {
            _context = context;
        }

        // Bound entity for form fields (DB entity).
        [BindProperty]
        public Player Player { get; set; }

        // Indicates whether the player has any games (controls whether PlayerId is editable).
        public bool HasGames { get; set; }

        // GET: /EditPlayer?id=123
        public IActionResult OnGet(int id)
        {
            // Load player by internal PK
            Player = _context.Players.FirstOrDefault(p => p.Id == id);
            if (Player == null)
                return NotFound();

            // Compute whether the player already has games
            HasGames = _context.Games.Any(g => g.PlayerId == id);

            return Page();
        }

        // POST: form submit
        public IActionResult OnPost()
        {
            // Validate basic DataAnnotations on Player
            if (!ModelState.IsValid)
            {
                // Recompute HasGames so the view renders PlayerId as readonly when appropriate
                HasGames = _context.Games.Any(g => g.PlayerId == Player.Id);
                return Page();
            }

            // Reload the tracked entity from DB
            var existingPlayer = _context.Players.FirstOrDefault(p => p.Id == Player.Id);
            if (existingPlayer == null)
                return NotFound();

            // Check if player already has games; if so, lock external PlayerId
            bool hasGames = _context.Games.Any(g => g.PlayerId == existingPlayer.Id);
            HasGames = hasGames; // keep for possible redisplay scenarios

            if (!hasGames)
            {
                // Note: uniqueness on external PlayerId is enforced by DB index.
                // If a duplicate is submitted, SaveChanges() will throw.
                // (Optional enhancement: pre-check and add a ModelState error instead of exception.)
                existingPlayer.PlayerId = Player.PlayerId;
            }

            // Update editable fields
            existingPlayer.FirstName = Player.FirstName;
            existingPlayer.Phone = Player.Phone;
            existingPlayer.Country = Player.Country;

            _context.SaveChanges();

            return RedirectToPage("/Queries");
        }
    }
}
