using Connect4_Server.Data;
using Connect4_Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Connect4_Server.Pages
{
    // Lightweight projection for game rows in the UI.
    public class GameDetails
    {
        public int GameId { get; set; }
        public string PlayerName { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }

        // Comma-separated list of columns from MoveList (ordered by MoveNumber).
        public string MovesColumns { get; set; } = string.Empty;

        // Optional raw CSV board snapshot from Game.Moves.
        public string? RawBoard { get; set; }
    }

    // For query #18
    public class PlayerLastGame
    {
        public string Name { get; set; }
        public DateTime? LastGameDate { get; set; }
    }

    // For query #22
    public class PlayerGameCount
    {
        public string Name { get; set; }
        public int GameCount { get; set; }
    }

    // For query #23 (grouping by number of games)
    public class PlayerGroupByGames
    {
        public int GameCount { get; set; }
        public List<Player> Players { get; set; }
    }

    // For query #24 (grouping by country)
    public class PlayerGroupByCountry
    {
        public string Country { get; set; }
        public List<Player> Players { get; set; }
    }

    // Queries page model: gathers all required query results for display.
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

        // GET parameters
        [BindProperty(SupportsGet = true)]
        public int? SelectedPlayerId { get; set; }

        // Selected player entity (used for detail card)
        public Player SelectedPlayer { get; set; }

        // GET: compute all sections. The 'sort' flag controls the initial Players ordering.
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

        // 17. Load players with sorting modes:
        // - insensitive: case-insensitive ascending
        // - sensitive: case-sensitive descending
        // - default: ascending by FirstName
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
                // Case-sensitive descending order (explicit collation)
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

        // Helper: resolve the SelectedPlayer (by external PlayerId)
        private void LoadSelectedPlayer()
        {
            if (SelectedPlayerId != null)
            {
                SelectedPlayer = _context.Players
                    .FirstOrDefault(p => p.PlayerId == SelectedPlayerId);
            }
        }

        // Load games for the selected player (with move columns)
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
                        MovesColumns = string.Join(
                            ",",
                            g.MoveList.OrderBy(m => m.MoveNumber).Select(m => m.Column)
                        ),
                        RawBoard = g.Moves
                    })
                    .ToList();
            }
        }

        // 22. Games count per player
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

        // 18. Last game date for each player; then order by name (case-sensitive DESC)
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
                // Case-sensitive, descending by name (Latin1_General_CS_AS suits SQL Server)
                .OrderByDescending(p => EF.Functions.Collate(p.Name, "Latin1_General_CS_AS"))
                .ToList();
        }

        // 19. All games with details
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
                    MovesColumns = string.Join(
                        ",",
                        g.MoveList.OrderBy(m => m.MoveNumber).Select(m => m.Column)
                    ),
                    RawBoard = g.Moves
                })
                .ToList();
        }

        // 20. Distinct games by player:
        // Uses client-side GroupBy (AsEnumerable) to pick a representative game per player.
        // Note: Fine for the assignment scale; for very large data, consider a server-side alternative.
        private void LoadUniqueGamesByPlayer()
        {
            UniqueGamesByPlayer = _context.Games
                .Include(g => g.Player)
                .Include(g => g.MoveList)
                .AsEnumerable() // switch to in-memory to use GroupBy on entities
                .GroupBy(g => g.PlayerId)
                .Select(g => g.OrderByDescending(x => x.StartTime).First())
                .Select(g => new GameDetails
                {
                    GameId = g.Id,
                    PlayerName = g.Player.FirstName,
                    StartTime = g.StartTime,
                    Duration = g.Duration,
                    MovesColumns = string.Join(
                        ",",
                        g.MoveList.OrderBy(m => m.MoveNumber).Select(m => m.Column)
                    ),
                    RawBoard = g.Moves
                })
                .ToList();
        }

        // 23. Players grouped by number of games (descending)
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

        // 24. Players grouped by country (ascending by country)
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

        // POST: delete a single game by internal Game.Id
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

        // POST: delete a player and all of their games
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

        // POST: launch client for server-side replay
        [ValidateAntiForgeryToken]
        public IActionResult OnPostReplay(int gameId, int playerId)
        {
            // 1) Client exe path from configuration (appsettings.json -> "ClientExePath")
            var exe = _config["ClientExePath"];
            if (string.IsNullOrWhiteSpace(exe) || !System.IO.File.Exists(exe))
            {
                TempData["Error"] = "ClientExePath not configured or file not found.";
                return RedirectToPage(new { SelectedPlayerId = playerId });
            }

            // 2) Build API base for the client
            var apiBase = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/GameApi/";

            // 3) Start the client with replay arguments
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
