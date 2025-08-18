using Connect4_Server.Data;
using Connect4_Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;



namespace Connect4_Server.Pages
{
    public class GameDetails
    {
        public int GameId { get; set; }
        public string PlayerName { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }

       
        public string MovesColumns { get; set; } = string.Empty;

      
        public string? RawBoard { get; set; }
    }


    public class PlayerLastGame
    {
        public string Name { get; set; }
        public DateTime? LastGameDate { get; set; }
    }

    public class PlayerGameCount
    {
        public string Name { get; set; }
        public int GameCount { get; set; }
    }

    public class PlayerGroupByGames
    {
        public int GameCount { get; set; }
        public List<Player> Players { get; set; }
    }

    public class PlayerGroupByCountry
    {
        public string Country { get; set; }
        public List<Player> Players { get; set; }
    }

    public class QueriesModel : PageModel
    {
        public List<GameDetails> UniqueGamesByPlayer { get; set; } = new();
        public List<GameDetails> SelectedPlayerGames { get; set; } = new();
        public List<GameDetails> AllGames { get; set; } = new();
        public List<PlayerLastGame> LastGameResults { get; set; } = new();
        public List<PlayerGameCount> PlayerGameCounts { get; set; } = new();
        public List<PlayerGroupByGames> GroupedPlayersByGameCount { get; set; } = new();
        public List<PlayerGroupByCountry> GroupedPlayersByCountry { get; set; } = new();
        public List<Player> Players { get; set; } = new();

        private readonly AppDbContext _context;
        private readonly IConfiguration _config;


        public QueriesModel(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [BindProperty(SupportsGet = true)]
        public int? SelectedPlayerId { get; set; }
        public Player SelectedPlayer { get; set; }

        public void OnGet(string sort = "none")
        {
            LoadPlayers(sort);
            LoadSelectedPlayer();
            LoadSelectedPlayerGames();
            LoadGameCounts();
            LoadLastGames();
            LoadAllGames();
            LoadUniqueGamesByPlayer();
            LoadGroupedPlayersByGameCount();
            LoadGroupedPlayersByCountry();
        }

        private void LoadPlayers(string sort)
        {
            var query = _context.Players.AsQueryable();

            if (sort == "insensitive")
            {
                // Case-insensitive ascending order
                query = query.OrderBy(p => p.FirstName.ToLower());
            }
            else if (sort == "sensitive")
            {
                // Case-sensitive descending order
                query = query.OrderByDescending(
                    p => EF.Functions.Collate(p.FirstName, "Latin1_General_CS_AS")
                );
            }
            else
            {
                // Default ordering
                query = query.OrderBy(p => p.FirstName);
            }

            Players = query.ToList();
        }


        private void LoadSelectedPlayer()
        {
            if (SelectedPlayerId != null)
            {
                SelectedPlayer = _context.Players
                    .FirstOrDefault(p => p.PlayerId == SelectedPlayerId);
            }
        }

        private void LoadSelectedPlayerGames()
        {
            if (SelectedPlayerId != null)
            {
                SelectedPlayerGames = _context.Games
                    .Include(g => g.Player)
                    .Include(g => g.MoveList)
                    .Where(g => g.Player.PlayerId == SelectedPlayerId)
                    .OrderByDescending(g => g.StartTime)
                    .Select(g => new GameDetails
                    {
                        GameId = g.Id,
                        PlayerName = g.Player.FirstName,
                        StartTime = g.StartTime,
                        Duration = g.Duration,
                        MovesColumns = string.Join(",", g.MoveList
                            .OrderBy(m => m.MoveNumber)
                            .Select(m => m.Column)),
                        RawBoard = g.Moves
                    })
                    .ToList();
            }
        }


        private void LoadGameCounts()
        {
            PlayerGameCounts = _context.Players
                .Select(p => new PlayerGameCount
                {
                    Name = p.FirstName,
                    GameCount = p.Games.Count()
                })
                .OrderByDescending(p => p.GameCount)
                .ThenBy(p => p.Name)
                .ToList();
        }

        private void LoadLastGames()
        {
            LastGameResults = _context.Players
                .Select(p => new PlayerLastGame
                {
                    Name = p.FirstName,
                    LastGameDate = p.Games
                        .OrderByDescending(g => g.StartTime)
                        .Select(g => (DateTime?)g.StartTime)
                        .FirstOrDefault()
                })
                .OrderByDescending(p => p.Name)
                .ToList();
        }

        private void LoadAllGames()
        {
            AllGames = _context.Games
                .Include(g => g.Player)
                .Include(g => g.MoveList)
                .OrderByDescending(g => g.StartTime)
                .Select(g => new GameDetails
                {
                    GameId = g.Id,
                    PlayerName = g.Player.FirstName,
                    StartTime = g.StartTime,
                    Duration = g.Duration,
                    MovesColumns = string.Join(",", g.MoveList
                        .OrderBy(m => m.MoveNumber)
                        .Select(m => m.Column)),
                    RawBoard = g.Moves
                })
                .ToList();
        }


        private void LoadUniqueGamesByPlayer()
        {
            UniqueGamesByPlayer = _context.Games
                .Include(g => g.Player)
                .Include(g => g.MoveList)
                .AsEnumerable()
                .GroupBy(g => g.PlayerId)
                .Select(g => g.OrderByDescending(x => x.StartTime).First())
                .Select(g => new GameDetails
                {
                    GameId = g.Id,
                    PlayerName = g.Player.FirstName,
                    StartTime = g.StartTime,
                    Duration = g.Duration,
                    MovesColumns = string.Join(",", g.MoveList
                        .OrderBy(m => m.MoveNumber)
                        .Select(m => m.Column)),
                    RawBoard = g.Moves
                })
                .ToList();
        }


        private void LoadGroupedPlayersByGameCount()
        {
            GroupedPlayersByGameCount = _context.Players
                .Select(p => new
                {
                    Player = p,
                    GameCount = p.Games.Count()
                })
                .GroupBy(p => p.GameCount)
                .OrderByDescending(g => g.Key)
                .Select(g => new PlayerGroupByGames
                {
                    GameCount = g.Key,
                    Players = g.Select(x => x.Player).ToList()
                })
                .ToList();
        }

        private void LoadGroupedPlayersByCountry()
        {
            GroupedPlayersByCountry = _context.Players
                .GroupBy(p => p.Country)
                .OrderBy(g => g.Key)
                .Select(g => new PlayerGroupByCountry
                {
                    Country = g.Key,
                    Players = g.ToList()
                })
                .ToList();
        }

        public IActionResult OnPostDeleteGame(int id)
        {
            var game = _context.Games.FirstOrDefault(g => g.Id == id);
            if (game != null)
            {
                _context.Games.Remove(game);
                _context.SaveChanges();
            }
            return RedirectToPage();
        }

        public IActionResult OnPostDeletePlayer(int id)
        {
            var player = _context.Players
                .Include(p => p.Games)
                .FirstOrDefault(p => p.Id == id);

            if (player != null)
            {
                _context.Games.RemoveRange(player.Games);
                _context.Players.Remove(player);
                _context.SaveChanges();
            }
            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public IActionResult OnPostReplay(int gameId, int playerId)
        {
            // 1) exe path מהקונפיג
            var exe = _config["ClientExePath"];
            if (string.IsNullOrWhiteSpace(exe) || !System.IO.File.Exists(exe))
            {
                TempData["Error"] = "ClientExePath not configured or file not found.";
                return RedirectToPage(new { SelectedPlayerId = playerId });
            }

            // 2) בסיס ה-API לקליינט
            var apiBase = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/GameApi/";

            // 3) הפעלת הקליינט עם פרמטרים
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"--replayServerGameId={gameId} --playerId={playerId} --api=\"{apiBase}\"",
                UseShellExecute = true,
                WorkingDirectory = System.IO.Path.GetDirectoryName(exe)
            };

            try
            {
                Process.Start(psi);
                TempData["Info"] = $"Launching replay for game #{gameId}…";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to launch client: " + ex.Message;
            }

            return RedirectToPage(new { SelectedPlayerId = playerId });
        }


    }
}
