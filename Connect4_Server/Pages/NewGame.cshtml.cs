using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System;
using Connect4_Server.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics; // Process

namespace Connect4_Server.Pages
{
    public class NewGameModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppDbContext _db;

        // Base URL of your server (https profile). We will derive the API base from this.
        private const string ServerBase = "https://localhost:7150/";
        // Full path to your client EXE. Adjust if your path is different.
        private const string ClientExePath = @"C:\Users\user\Connect4\Connect4\Connect4_Client\bin\Debug\Connect4_Client.exe";

        public NewGameModel(IHttpClientFactory httpClientFactory, AppDbContext db)
        {
            _httpClientFactory = httpClientFactory;
            _db = db;
        }

        // External PlayerId (1..1000) that /api/GameApi/start expects
        [BindProperty]
        public int PlayerId { get; set; }

        // Display-only metadata
        public string? PlayerName { get; set; }
        public int GameId { get; set; }
        public string? StatusMessage { get; set; }

        public async Task OnGetAsync()
        {
            // ???? ???? ?? ?-PK ?????? ??????/????? ??????
            var dbPk = HttpContext.Session.GetInt32("CurrentPlayerDbId");
            // ???? ?? ?? ?-ID ??????? (?? ????????? ?????: ????? ????? PK)
            var sessId = HttpContext.Session.GetInt32("CurrentPlayerId");

            Models.Player? player = null;

            // 1) ??? DB PK (???? ??????)
            if (dbPk is not null)
                player = await _db.Players.FirstOrDefaultAsync(p => p.Id == dbPk.Value);

            // 2) ?? ?? ????: ??? ???? ?? CurrentPlayerId ?-ExternalId (1..1000)
            if (player is null && sessId is not null)
                player = await _db.Players.FirstOrDefaultAsync(p => p.PlayerId == sessId.Value);

            // 3) ?????? ?????: ????? ??-session ??? PK ??? CurrentPlayerId
            if (player is null && sessId is not null)
                player = await _db.Players.FirstOrDefaultAsync(p => p.Id == sessId.Value);

            if (player is null)
            {
                StatusMessage = "No logged-in player found. Please login first.";
                return;
            }

            // ????? ???? ???? (??? ??? ????? ????? ???????)
            HttpContext.Session.SetInt32("CurrentPlayerDbId", player.Id);        // DB PK
            HttpContext.Session.SetInt32("CurrentPlayerId", player.PlayerId);    // External ID (1..1000)
            HttpContext.Session.SetString("CurrentPlayerName", player.FirstName);

            // ????? ?????? ??????? ?-API
            PlayerId = player.PlayerId;     // external id to send to /start
            PlayerName = player.FirstName;
        }

        public async Task<IActionResult> OnPostStartGameAsync()
        {
            if (PlayerId <= 0)
            {
                ModelState.AddModelError(string.Empty, "Missing PlayerId. Please login and try again.");
                return Page();
            }

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(ServerBase);

            // POST api/GameApi/start -> returns GameStateDto (we only need GameId)
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
                // Keep for reference if needed by website
                HttpContext.Session.SetInt32("CurrentGameId", GameId);

                // Launch the WinForms client and pass arguments
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
                    // If launching fails, still show the Game ID so you can start client manually
                    StatusMessage = $"Started game (ID {GameId}) but failed to launch client: {ex.Message}";
                }
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Server did not return a valid GameId.");
            }

            return Page();
        }

        public class StartGameResponse
        {
            public int GameId { get; set; }
        }
    }
}
