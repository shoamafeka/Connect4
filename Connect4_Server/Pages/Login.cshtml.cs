using Connect4_Server.Data;
using Connect4_Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Connect4_Server.Pages
{
    // Login page model:
    // - Lists all registered players (ordered by first name).
    // - When a player is selected, stores login session keys:
    //   "CurrentPlayerId" (int, internal DB Id) and "CurrentPlayerName" (string).
    public class LoginModel : PageModel
    {
        private readonly AppDbContext _context;

        public LoginModel(AppDbContext context)
        {
            _context = context;
        }

        // Players rendered in the view for selection.
        public List<Player> Players { get; set; } = new();

        // GET: load all players for selection.
        public async Task OnGetAsync()
        {
            Players = await _context.Players
                .OrderBy(p => p.FirstName)
                .ToListAsync();
        }

        // POST: invoked when clicking "Select" in a specific row (hidden playerId = internal DB Id).
        public IActionResult OnPost(int playerId)
        {
            var player = _context.Players.FirstOrDefault(p => p.Id == playerId);
            if (player != null)
            {
                // Session keys used by the layout and other pages to detect login state and greet the user.
                HttpContext.Session.SetInt32("CurrentPlayerId", player.Id);
                HttpContext.Session.SetString("CurrentPlayerName", player.FirstName);
            }

            // Redirect to home page after selection (even if player not found, to keep UX simple).
            return RedirectToPage("/Index");
        }
    }
}
