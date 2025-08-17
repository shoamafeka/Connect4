using Connect4_Server.Data;
using Connect4_Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Connect4_Server.Pages
{
    public class LoginModel : PageModel
    {
        private readonly AppDbContext _context;

        public LoginModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Player> Players { get; set; } = new();

        public async Task OnGetAsync()
        {
            Players = await _context.Players.OrderBy(p => p.FirstName).ToListAsync();
        }

        public IActionResult OnPost(int playerId)
        {
            var player = _context.Players.FirstOrDefault(p => p.Id == playerId);
            if (player != null)
            {
                HttpContext.Session.SetInt32("CurrentPlayerId", player.Id);
                HttpContext.Session.SetString("CurrentPlayerName", player.FirstName);
            }

            return RedirectToPage("/Index");
        }
    }
}
