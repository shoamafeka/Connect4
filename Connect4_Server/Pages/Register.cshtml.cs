using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Connect4_Server.Models;
using Connect4_Server.Data;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace Connect4_Server.Pages
{
    // Registration page model:
    // - Validates new player using DataAnnotations + custom messages.
    // - Ensures external PlayerId uniqueness.
    // - On success, stores login session keys and redirects back to the form with a success message.
    public class RegisterModel : PageModel
    {
        private readonly AppDbContext _context;

        public RegisterModel(AppDbContext context)
        {
            _context = context;
        }

        // Bound entity for the form.
        [BindProperty]
        public Player Player { get; set; } = new Player();

        public IActionResult OnPost()
        {
            // Server-side validation
            if (!ModelState.IsValid)
                return Page();

            // Enforce uniqueness on external PlayerId (1..1000)
            if (_context.Players.Any(p => p.PlayerId == Player.PlayerId))
            {
                ModelState.AddModelError("Player.PlayerId", "This Player ID is already taken. Please choose another.");
                return Page();
            }

            // Persist the new player
            _context.Players.Add(Player);
            _context.SaveChanges();

            // Session keys (keep consistent with Login/NewGame/GameApi):
            // - CurrentPlayerId   => INTERNAL DB Id (used app-wide)
            // - CurrentPlayerDbId => INTERNAL DB Id (kept for legacy use if referenced elsewhere)
            // - CurrentPlayerName => First name
            HttpContext.Session.SetInt32("CurrentPlayerId", Player.Id);      // internal DB Id (consistency)
            HttpContext.Session.SetInt32("CurrentPlayerDbId", Player.Id);    // same as above
            HttpContext.Session.SetString("CurrentPlayerName", Player.FirstName);

            // Success message (rendered by the view on redirect)
            TempData["StatusMessage"] = $"🎉 Player '{Player.FirstName}' registered successfully!";

            return RedirectToPage("/Register");
        }
    }
}
