using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Connect4_Server.Models;
using Connect4_Server.Data;
using System.Linq;

namespace Connect4_Server.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly AppDbContext _context;

        public RegisterModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Player Player { get; set; }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
                return Page();

            if (_context.Players.Any(p => p.PlayerId == Player.PlayerId))
            {
                ModelState.AddModelError("Player.PlayerId", "This Player ID already exists.");
                return Page();
            }

            _context.Players.Add(Player);
            _context.SaveChanges();

            TempData["StatusMessage"] = $"🎉 Player {Player.FirstName} registered!";
            return RedirectToPage("/Index");
        }






    }
}
