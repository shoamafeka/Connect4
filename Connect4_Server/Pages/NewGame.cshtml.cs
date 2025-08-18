using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System;
using Connect4_Server.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics; // Process

namespace Connect4_Server.Pages
{
    // NewGame page model:
    // - Reads the currently logged-in player from Session (CurrentPlayerId = internal DB Id).
    // - Displays player name + external PlayerId (1..1000).
    // - On POST, calls API /api/GameApi/start with the external PlayerId, and launches the WinForms client.
    public class NewGameModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppDbContext _db;

        // Base URL of this server (https). API base is derived from it.
        private const string ServerBase = "https://localhost:7150/";

        // Full path to the WinForms client EXE. Adjust to your machine if needed.
        private const string ClientExePath = @"C:\Users\user\Connect4\Connect4\Connect4_Client\bin\Debug\Connect4_Client.exe";

        public NewGameModel(IHttpClientFactory httpClientFactory, AppDbContext db)
        {
            _httpClientFactory = httpClientFactory;
            _db = db;
        }

        // External PlayerId (1..1000) expected by /api/GameApi/start
        [BindProperty]
        public int PlayerId { get; set; }

        // Display-only metadata
        public string? PlayerName { get; set; }
        public int GameId { get; set; }
        public string? StatusMessage { get; set; }

        // GET: resolve the currently logged-in player from Session and populate display fields.
        public async Task OnGetAsync()
        {
            // Session convention in this app:
            // - "CurrentPlayerId" stores the INTERNAL DB Id (set by Login).
            // - "CurrentPlayerName" stores the player's first name.
            var dbId = HttpContext.Session.GetInt32("CurrentPlayerId");
            if (dbId is null)
            {
                StatusMessage = "No logged-in player found. Please login first.";
                return;
            }

            // Load player by internal PK
            var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == dbId.Value);
            if (player is null)
            {
                StatusMessage = "No logged-in player found. Please login first.";
                return;
            }

            // Keep session keys consistent (internal Id + name). Do NOT overwrite CurrentPlayerId with external ID.
            HttpContext.Session.SetInt32("CurrentPlayerId", player.Id);      // internal DB Id
            HttpContext.Session.SetString("CurrentPlayerName", player.FirstName);

            // Populate fields for the page and for POST to the API
            PlayerId = player.PlayerId;   // external 1..1000 (sent to /start)
            PlayerName = player.FirstName;
        }

        // POST (handler: StartGame): call API /api/GameApi/start and try to launch the client app.
        public async Task<IActionResult> OnPostStartGameAsync()
        {
            if (PlayerId <= 0)
            {
                ModelState.AddModelError(string.Empty, "Missing PlayerId. Please login and try again.");
                return Page();
            }

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(ServerBase);

            // POST api/GameApi/start -> returns GameStateDto with GameId
            var response = await client.PostAsJsonAsync("api/GameApi/start", new { PlayerId = PlayerId });
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode == 404)
                    ModelState.AddModelError(string.Empty, "Player not found on server.");
                else
                    ModelState.AddModelError(string.Empty, $"Error starting game: {response.StatusCode}");
                return Page();
            }

            var data = await response.Content.ReadFromJsonAsync<StartGameResponse>();
            GameId = data?.GameId ?? 0;

            if (GameId > 0)
            {
                // Keep GameId in session for potential future use on the website
                HttpContext.Session.SetInt32("CurrentGameId", GameId);

                // Prepare arguments for the client EXE
                string apiBase = ServerBase.TrimEnd('/') + "/api/GameApi/";
                string args = $"--gameId={GameId} --playerId={PlayerId} --api=\"{apiBase}\"";

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = ClientExePath,
                        Arguments = args,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    // If launching fails, still show the Game ID so the client can be started manually.
                    StatusMessage = $"Started game (ID {GameId}) but failed to launch client: {ex.Message}";
                }
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Server did not return a valid GameId.");
            }

            return Page();
        }

        // Minimal shape of the response from /api/GameApi/start
        public class StartGameResponse
        {
            public int GameId { get; set; }
        }
    }
}
