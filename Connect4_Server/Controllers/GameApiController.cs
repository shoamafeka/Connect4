using Connect4_Server.Data;
using Connect4_Server.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;


namespace Connect4_Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private const int Rows = 6;
        private const int Cols = 7;

        public GameApiController(AppDbContext context) => _context = context;

        // GET: api/GameApi/player/{playerId}
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

            if (p == null)
                return NotFound(new { message = "Player not found" });

            return Ok(p);
        }

        // GET: api/GameApi/current
        [HttpGet("current")]
        public IActionResult GetCurrentPlayer()
        {
            var currentPlayerId = HttpContext.Session.GetInt32("CurrentPlayerId");
            if (currentPlayerId == null)
            {
                return Unauthorized(new { message = "No player is currently logged in." });
            }

            var player = _context.Players
                .Where(p => p.Id == currentPlayerId.Value)
                .Select(p => new PlayerDto
                {
                    Id = p.Id,
                    PlayerId = p.PlayerId,
                    FirstName = p.FirstName,
                    Phone = p.Phone,
                    Country = p.Country
                })
                .FirstOrDefault();

            if (player == null)
            {
                return NotFound(new { message = "Player not found." });
            }

            return Ok(player);
        }

        // POST: api/GameApi/start
        [HttpPost("start")]
        public IActionResult StartGame([FromBody] StartGameRequest request)
        {
            var player = _context.Players.FirstOrDefault(p => p.PlayerId == request.PlayerId);
            if (player == null)
                return NotFound(new { message = "Player not found" });

            // Initialize empty board (0s)
            var board = new int[Rows, Cols];
            var serialized = SerializeBoard(board);

            var game = new Game
            {
                // IMPORTANT: FK is Player.Id (DB PK), not Player.PlayerId (external)
                PlayerId = player.Id,
                StartTime = DateTime.UtcNow,
                Moves = serialized,
                Result = GameResult.Unknown
            };

            _context.Games.Add(game);
            _context.SaveChanges();

            var state = new GameStateDto
            {
                GameId = game.Id,
                Board = ToJagged(board),   // JSON-safe jagged array
                CurrentPlayer = 1,
                Status = "ongoing"
            };

            return Ok(state);
        }

        // POST: api/GameApi/move
        [HttpPost("move")]
        public IActionResult MakeMove([FromBody] MoveRequest request)
        {
            var game = _context.Games.FirstOrDefault(g => g.Id == request.GameId);
            if (game == null)
                return NotFound(new { message = "Game not found" });

            var board = ParseBoard(game.Moves);

            // ---- Helper to get next move number ----
            int NextMoveNumber()
            {
                // שואב את המקסימום הקיים למשחק הזה ומוסיף 1
                var max = _context.Moves.Where(m => m.GameId == game.Id)
                                        .Select(m => (int?)m.MoveNumber)
                                        .Max() ?? 0;
                return max + 1;
            }

            // ---- HUMAN move ----
            if (!DropDisc(board, request.Column, 1))
                return BadRequest(new { message = "Invalid move" });

            // שמירת מהלך השחקן (עמודה 0..6)
            _context.Moves.Add(new Move
            {
                GameId = game.Id,
                MoveNumber = NextMoveNumber(),
                Column = request.Column,
                IsPlayerMove = true
            });

            if (CheckWin(board, 1))
            {
                game.Moves = SerializeBoard(board);
                game.Result = GameResult.HumanWin;
                game.Duration = DateTime.UtcNow - game.StartTime;
                _context.SaveChanges();

                var movesList = _context.Moves.Where(m => m.GameId == game.Id)
                    .OrderBy(m => m.MoveNumber)
                    .Select(m => new MoveDto { MoveNumber = m.MoveNumber, Column = m.Column, IsPlayerMove = m.IsPlayerMove })
                    .ToList();

                return Ok(new MoveResponse { Board = ToJagged(board), CurrentPlayer = 1, Status = "player_won", Moves = movesList });
            }

            if (IsDraw(board))
            {
                game.Moves = SerializeBoard(board);
                game.Result = GameResult.Draw;
                game.Duration = DateTime.UtcNow - game.StartTime;
                _context.SaveChanges();

                var movesList = _context.Moves.Where(m => m.GameId == game.Id)
                    .OrderBy(m => m.MoveNumber)
                    .Select(m => new MoveDto { MoveNumber = m.MoveNumber, Column = m.Column, IsPlayerMove = m.IsPlayerMove })
                    .ToList();

                return Ok(new MoveResponse { Board = ToJagged(board), CurrentPlayer = 0, Status = "draw", Moves = movesList });
            }

            // ---- SERVER random move ----
            var rnd = new Random();
            var legalCols = Enumerable.Range(0, Cols).Where(c => board[0, c] == 0).ToList();
            if (legalCols.Count > 0)
            {
                var serverCol = legalCols[rnd.Next(legalCols.Count)];
                DropDisc(board, serverCol, 2);

                // שמירת מהלך השרת
                _context.Moves.Add(new Move
                {
                    GameId = game.Id,
                    MoveNumber = NextMoveNumber(),
                    Column = serverCol,
                    IsPlayerMove = false
                });

                if (CheckWin(board, 2))
                {
                    game.Moves = SerializeBoard(board);
                    game.Result = GameResult.ServerWin;
                    game.Duration = DateTime.UtcNow - game.StartTime;
                    _context.SaveChanges();

                    var movesList = _context.Moves.Where(m => m.GameId == game.Id)
                        .OrderBy(m => m.MoveNumber)
                        .Select(m => new MoveDto { MoveNumber = m.MoveNumber, Column = m.Column, IsPlayerMove = m.IsPlayerMove })
                        .ToList();

                    return Ok(new MoveResponse { Board = ToJagged(board), CurrentPlayer = 2, Status = "server_won", Moves = movesList });
                }
            }

            if (IsDraw(board))
            {
                game.Moves = SerializeBoard(board);
                game.Result = GameResult.Draw;
                game.Duration = DateTime.UtcNow - game.StartTime;
                _context.SaveChanges();

                var movesList = _context.Moves.Where(m => m.GameId == game.Id)
                    .OrderBy(m => m.MoveNumber)
                    .Select(m => new MoveDto { MoveNumber = m.MoveNumber, Column = m.Column, IsPlayerMove = m.IsPlayerMove })
                    .ToList();

                return Ok(new MoveResponse { Board = ToJagged(board), CurrentPlayer = 0, Status = "draw", Moves = movesList });
            }

            // Ongoing
            game.Moves = SerializeBoard(board);
            _context.SaveChanges();

            var ongoingMoves = _context.Moves.Where(m => m.GameId == game.Id)
                .OrderBy(m => m.MoveNumber)
                .Select(m => new MoveDto { MoveNumber = m.MoveNumber, Column = m.Column, IsPlayerMove = m.IsPlayerMove })
                .ToList();

            return Ok(new MoveResponse { Board = ToJagged(board), CurrentPlayer = 1, Status = "ongoing", Moves = ongoingMoves });
        }


        // GET: api/GameApi/{gameId}
        [HttpGet("{gameId:int}")]
        public IActionResult GetGame(int gameId)
        {
            var game = _context.Games.FirstOrDefault(g => g.Id == gameId);
            if (game == null)
                return NotFound(new { message = "Game not found" });

            var board = ParseBoard(game.Moves);

            var state = new GameStateDto
            {
                GameId = game.Id,
                Board = ToJagged(board), // JSON-safe jagged array
                CurrentPlayer = (game.Result == GameResult.Unknown) ? 1 : 0,
                Status = game.Result switch
                {
                    GameResult.Unknown => "ongoing",
                    GameResult.HumanWin => "player_won",
                    GameResult.ServerWin => "server_won",
                    GameResult.Draw => "draw",
                    _ => "ongoing"
                },
                Moves = _context.Moves.Where(m => m.GameId == game.Id)
                    .OrderBy(m => m.MoveNumber)
                    .Select(m => new MoveDto { MoveNumber = m.MoveNumber, Column = m.Column, IsPlayerMove = m.IsPlayerMove })
                    .ToList()
            };

            return Ok(state);
        }


        // --- Helpers (board/serialization) ---
        private bool DropDisc(int[,] board, int col, int player)
        {
            for (int row = Rows - 1; row >= 0; row--)
            {
                if (board[row, col] == 0)
                {
                    board[row, col] = player;
                    return true;
                }
            }
            return false;
        }

        private bool CheckWin(int[,] board, int player)
        {
            int[][] directions = {
                new[] { 0, 1 }, new[] { 1, 0 },
                new[] { 1, 1 }, new[] { 1, -1 }
            };

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    if (board[r, c] != player) continue;
                    foreach (var d in directions)
                    {
                        int count = 1;
                        int nr = r + d[0], nc = c + d[1];
                        while (nr >= 0 && nr < Rows && nc >= 0 && nc < Cols && board[nr, nc] == player)
                        {
                            count++;
                            if (count >= 4) return true;
                            nr += d[0]; nc += d[1];
                        }
                    }
                }
            }
            return false;
        }

        private bool IsDraw(int[,] board)
        {
            for (int c = 0; c < Cols; c++)
                if (board[0, c] == 0) return false;
            return true;
        }

        private int[,] ParseBoard(string moves)
        {
            var flat = moves.Split(',').Select(int.Parse).ToArray();
            var board = new int[Rows, Cols];
            for (int i = 0; i < flat.Length; i++)
                board[i / Cols, i % Cols] = flat[i];
            return board;
        }

        private string SerializeBoard(int[,] board)
        {
            var flat = new List<int>(Rows * Cols);
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    flat.Add(board[r, c]);
            return string.Join(",", flat);
        }

        private static int[][] ToJagged(int[,] board)
        {
            var result = new int[Rows][];
            for (int r = 0; r < Rows; r++)
            {
                result[r] = new int[Cols];
                for (int c = 0; c < Cols; c++)
                    result[r][c] = board[r, c];
            }
            return result;
        }
    }

    // --- DTOs ---
    public class PlayerDto
    {
        public int Id { get; set; }         // DB PK
        public int PlayerId { get; set; }   // External ID (1..1000)
        public string FirstName { get; set; }
        public string Phone { get; set; }
        public string Country { get; set; }
    }

    public class StartGameRequest
    {
        public int PlayerId { get; set; }   // External ID (1..1000)
    }

    public class MoveRequest
    {
        public int GameId { get; set; }
        public int Column { get; set; }
    }

    public class MoveResponse
    {
        public int[][] Board { get; set; }  // Jagged for JSON
        public int CurrentPlayer { get; set; }
        public string Status { get; set; }
        public List<MoveDto> Moves { get; set; } = new(); // <-- חדש
    }

    public class GameStateDto
    {
        public int GameId { get; set; }
        public int[][] Board { get; set; }  // Jagged for JSON
        public int CurrentPlayer { get; set; }
        public string Status { get; set; }
        public List<MoveDto> Moves { get; set; } = new(); // <-- חדש
    }


    public class MoveDto
    {
        public int MoveNumber { get; set; } // 1,2,3...
        public int Column { get; set; }     // 0..6
        public bool IsPlayerMove { get; set; } // true=Human, false=Server
    }


}
