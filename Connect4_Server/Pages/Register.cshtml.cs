using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Connect4_Server.Models;
using Connect4_Server.Data;
using System.Linq;
using Microsoft.AspNetCore.Http; // בשביל Session

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
        public Player Player { get; set; } = new Player();

        public IActionResult OnPost()
        {
            // Server-side validation
            if (!ModelState.IsValid)
                return Page();

            // Ensure unique PlayerId
            if (_context.Players.Any(p => p.PlayerId == Player.PlayerId))
            {
                ModelState.AddModelError("Player.PlayerId", "This Player ID is already taken. Please choose another.");
                return Page();
            }

            // Register.cshtml.cs  
            _context.Players.Add(Player);
            _context.SaveChanges();

            
            HttpContext.Session.SetInt32("CurrentPlayerId", Player.PlayerId);   // External ID 1..1000
            HttpContext.Session.SetInt32("CurrentPlayerDbId", Player.Id);       // DB PK
            HttpContext.Session.SetString("CurrentPlayerName", Player.FirstName);

           
            TempData["StatusMessage"] = $"🎉 Player '{Player.FirstName}' registered successfully!";

           
            return RedirectToPage("/Register");

        }
    }
}
