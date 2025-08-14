using Connect4_Server.Data;
using Connect4_Server.Models;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace Connect4_Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        public GameApiController(AppDbContext context) => _context = context;

        // GET: api/GameApi/player/123   (123 is PlayerId from the form, not DB Id)
        [HttpGet("player/{playerId:int}")]
        public IActionResult GetPlayer(int playerId)
        {
            var p = _context.Players
                .Where(x => x.PlayerId == playerId)
                .Select(x => new PlayerDto
                {
                    Id = x.Id,
                    PlayerId = x.PlayerId,
                    FirstName = x.FirstName,
                    Phone = x.Phone,
                    Country = x.Country
                })
                .FirstOrDefault();

            if (p == null) return NotFound(new { message = "Player not found" });
            return Ok(p);
        }
    }

    public class PlayerDto
    {
        public int Id { get; set; }          // DB Id
        public int PlayerId { get; set; }    // public displayed ID (1–1000)
        public string FirstName { get; set; }
        public string Phone { get; set; }
        public string Country { get; set; }
    }
}
