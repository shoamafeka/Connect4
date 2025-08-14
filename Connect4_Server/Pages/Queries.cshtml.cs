using Connect4_Server.Data;
using Connect4_Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace Connect4_Server.Pages
{

    // For query 19 Displaying all games with all details.
    public class GameDetails
    {
        public int GameId { get; set; }
        public string PlayerName { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public string Moves { get; set; }
    }
    // For query 18
    //Displaying all players with descending order sorting by name but case sensitivity
    //,when only the names should be displayed and
    //in addition only the date of the last game they played. (i.e., only two columns)
    public class PlayerLastGame
    {
        public string Name { get; set; }
        public DateTime? LastGameDate { get; set; }


    }
    // For query 22
    public class PlayerGameCount
    {
        public string Name { get; set; }
        public int GameCount { get; set; }
    }

    // For query 23, we can't use the PlayerGameCount class directly
    // because it doesn't include all the players that playes the same number of games.
    public class PlayerGroupByGames
    {
        public int GameCount { get; set; }  //0,1,2,3
        public List<Player> Players { get; set; }
    }
    // For query 24
    public class PlayerGroupByCountry
    {
        public string Country { get; set; }
        public List<Player> Players { get; set; }
    }


    public class QueriesModel : PageModel
    {
        public List<GameDetails> UniqueGamesByPlayer { get; set; } = new();
        public List<Game> SelectedPlayerGames { get; set; } = new List<Game>();
        public List<GameDetails> AllGames { get; set; } = new();
        public List<PlayerLastGame> LastGameResults { get; set; } = new();
        public List<PlayerGameCount> PlayerGameCounts { get; set; } = new();
        public List<PlayerGroupByGames> GroupedPlayersByGameCount { get; set; } = new List<PlayerGroupByGames>();
        public List<PlayerGroupByCountry> GroupedPlayersByCountry { get; set; } = new List<PlayerGroupByCountry>();


        private readonly AppDbContext _context;

        public QueriesModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Player> Players { get; set; } = new List<Player>();

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
                query = query.OrderBy(p => p.FirstName.ToLower());
            else if (sort == "sensitive")
                query = query.OrderBy(p => p.FirstName);

            Players = query.ToList();
        }

        private void LoadSelectedPlayer()
        {
            if (SelectedPlayerId != null)
            {
                SelectedPlayer = _context.Players.FirstOrDefault(p => p.PlayerId == SelectedPlayerId);
            }
        }

        private void LoadSelectedPlayerGames()
        {
            if (SelectedPlayerId != null)
            {
                SelectedPlayerGames = _context.Games
                    .Where(g => g.Player.PlayerId == SelectedPlayerId)
                    .OrderByDescending(g => g.StartTime)
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
                .Select(g => new GameDetails
                {
                    GameId = g.Id,
                    PlayerName = g.Player.FirstName,
                    StartTime = g.StartTime,
                    Duration = g.Duration,
                    Moves = g.Moves
                })
                .OrderByDescending(g => g.StartTime)
                .ToList();
        }

        private void LoadUniqueGamesByPlayer()
        {
            UniqueGamesByPlayer = _context.Games
                .Include(g => g.Player)
                .AsEnumerable()
                .GroupBy(g => g.PlayerId)
                .Select(g => g.OrderByDescending(x => x.StartTime).First())
                .Select(g => new GameDetails
                {
                    GameId = g.Id,
                    PlayerName = g.Player.FirstName,
                    StartTime = g.StartTime,
                    Duration = g.Duration,
                    Moves = g.Moves
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

            return RedirectToPage(); // Refresh page
        }
        
        public IActionResult OnPostDeletePlayer(int id)
        {
            var player = _context.Players.Include(p => p.Games).FirstOrDefault(p => p.Id == id);

            if (player != null)
            {
                // Remove all games associated with the player
                _context.Games.RemoveRange(player.Games);
                _context.Players.Remove(player);
                _context.SaveChanges();
            }
            return RedirectToPage(); // Refresh page
        }


    }
}