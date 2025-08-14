using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Connect4_Server.Models;
using Connect4_Server.Data;
using System.Linq;


namespace Connect4_Server.Pages
{
    
    public class EditPlayerModel : PageModel
    {
        private readonly AppDbContext _context;
        public EditPlayerModel(AppDbContext context)
        {
            _context = context;
        }


        [BindProperty]
        public Player Player { get; set; }

        public bool HasGames { get; set; }

        public IActionResult OnGet(int id)
        {
            Player = _context.Players.FirstOrDefault(p => p.Id == id);
            if (Player == null)
            {
                return NotFound();
            }
            HasGames = _context.Games.Any(g => g.PlayerId == id);
            return Page();
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            var existingPlayer = _context.Players.FirstOrDefault(p => p.Id == Player.Id);
            if (existingPlayer == null)
                return NotFound();

            // Conditionally allow updating PlayerId
            bool hasGames = _context.Games.Any(g => g.PlayerId == existingPlayer.Id);

            if (!hasGames)
                existingPlayer.PlayerId = Player.PlayerId;

            existingPlayer.FirstName = Player.FirstName;
            existingPlayer.Phone = Player.Phone;
            existingPlayer.Country = Player.Country;

            _context.SaveChanges();

            return RedirectToPage("/Queries");
        }


    }
}
