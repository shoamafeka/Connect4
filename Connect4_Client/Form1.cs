using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;               // for StringContent (UTF8)
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;           // use Newtonsoft on .NET Framework

namespace Connect4_Client
{
    public partial class Form1 : Form
    {
        // --- Board constants ---
        private const int Rows = 6;
        private const int Cols = 7;
        private const int CellSize = 60;
        private const int TopMargin = 40;   // space for the info label

        // --- API base URL (override via --api=... if provided) ---
        private string _apiBase = "https://localhost:7150/api/GameApi/";

        // --- State ---
        private int[,] _board = new int[Rows, Cols];       // current visible board (used for drawing/animation)
        private int[,] _targetBoard = new int[Rows, Cols]; // final board after server response
        private int? _currentGameId;
        private int _externalPlayerId;                     // PlayerId 1..1000
        private string _playerName = "";
        private string _playerPhone = "";
        private string _playerCountry = "";
        private bool _isAnimating = false;
        private bool _gameOver = false;

        // --- Animation ---
        private readonly Timer _dropTimer = new Timer();
        private readonly Queue<DropAnim> _animQueue = new Queue<DropAnim>();
        private DropAnim _currentAnim;
        private string _pendingEndStatus = null;

        // --- UI ---
        private Label _lblInfo;

        // --- HTTP ---
        private HttpClient _http; // will be created after _apiBase is finalized

        public Form1()
        {
            InitializeComponent();
            this.Load += Form1_Load;
        }

        // Simple struct to drive a single falling disc animation
        private class DropAnim
        {
            public int Player;   // 1 = human, 2 = server
            public int Col;
            public int TargetRow;
            public int CurrentRow;
        }

        // --- DTOs (scoped for this form to avoid name clashes) ---
        private class ApiStartGameRequest { public int PlayerId { get; set; } }
        private class ApiStartGameResponse
        {
            public int GameId { get; set; }
            public int[][] Board { get; set; }
            public int CurrentPlayer { get; set; }
            public string Status { get; set; }
        }
        private class ApiMoveRequest { public int GameId { get; set; } public int Column { get; set; } }
        private class ApiMoveResponse
        {
            public int[][] Board { get; set; }
            public int CurrentPlayer { get; set; }
            public string Status { get; set; }
        }
        private class ApiGameStateResponse
        {
            public int GameId { get; set; }
            public int[][] Board { get; set; }
            public int CurrentPlayer { get; set; }
            public string Status { get; set; }
        }
        private class ApiPlayerDto
        {
            public int Id { get; set; }        // DB PK
            public int PlayerId { get; set; }  // External ID (1..1000)
            public string FirstName { get; set; }
            public string Phone { get; set; }
            public string Country { get; set; }
        }

        // Helper: convert jagged to 2D
        private static int[,] To2D(int[][] jagged)
        {
            var r = jagged.Length;
            var c = jagged[0].Length;
            var result = new int[r, c];
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    result[i, j] = jagged[i][j];
            return result;
        }

        // Parse command-line args: --gameId=123 --playerId=45 --api="https://.../api/GameApi/"
        private (int? gameId, int? playerId, string api) ParseArgs()
        {
            int? gameId = null;
            int? playerId = null;
            string api = null;

            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("--gameId=", StringComparison.OrdinalIgnoreCase))
                {
                    var val = arg.Substring("--gameId=".Length).Trim('"');
                    if (int.TryParse(val, out int g)) gameId = g;
                }
                else if (arg.StartsWith("--playerId=", StringComparison.OrdinalIgnoreCase))
                {
                    var val = arg.Substring("--playerId=".Length).Trim('"');
                    if (int.TryParse(val, out int p)) playerId = p;
                }
                else if (arg.StartsWith("--api=", StringComparison.OrdinalIgnoreCase))
                {
                    api = arg.Substring("--api=".Length).Trim('"');
                }
            }
            return (gameId, playerId, api);
        }

        // Prompt for external PlayerId (1..1000) when not provided via args
        private int PromptForExternalPlayerId()
        {
            using (var dlg = new Form())
            using (var txt = new TextBox())
            using (var ok = new Button())
            using (var cancel = new Button())
            using (var lbl = new Label())
            {
                dlg.Text = "Enter Player ID (1..1000)";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.ClientSize = new Size(300, 130);
                dlg.MinimizeBox = false;
                dlg.MaximizeBox = false;
                dlg.ShowInTaskbar = false;

                lbl.Text = "External PlayerId (1..1000):";
                lbl.AutoSize = true;
                lbl.Location = new Point(12, 15);

                txt.Location = new Point(15, 40);
                txt.Width = 260;

                ok.Text = "OK";
                ok.Location = new Point(120, 80);
                ok.DialogResult = DialogResult.OK;

                cancel.Text = "Cancel";
                cancel.Location = new Point(200, 80);
                cancel.DialogResult = DialogResult.Cancel;

                dlg.Controls.Add(lbl);
                dlg.Controls.Add(txt);
                dlg.Controls.Add(ok);
                dlg.Controls.Add(cancel);
                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (int.TryParse(txt.Text, out int pid) && pid >= 1 && pid <= 1000)
                        return pid;
                }
                return 0;
            }
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            // UI sizing and double buffer
            this.DoubleBuffered = true;
            this.ClientSize = new Size(Cols * CellSize, TopMargin + Rows * CellSize);

            _lblInfo = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 7, FontStyle.Bold),
                ForeColor = Color.Black,
                Location = new Point(10, 10)
            };
            this.Controls.Add(_lblInfo);

            _dropTimer.Interval = 100; // animation speed
            _dropTimer.Tick += DropTimer_Tick;

            // Parse args (may include gameId, playerId, api)
            var (argGameId, argPlayerId, argApi) = ParseArgs();
            if (!string.IsNullOrWhiteSpace(argApi))
            {
                _apiBase = argApi.Trim();
                if (!_apiBase.EndsWith("/")) _apiBase += "/";
            }
            _http = new HttpClient { BaseAddress = new Uri(_apiBase) };

            // If we got a GameId from the website, load that existing game and start immediately
            if (argGameId.HasValue && argGameId.Value > 0)
            {
                // Optional: if playerId provided, fetch player name to show
                if (argPlayerId.HasValue && argPlayerId.Value > 0)
                {
                    _externalPlayerId = argPlayerId.Value;
                    try
                    {
                        var resp = await _http.GetAsync($"player/{_externalPlayerId}");
                        if (resp.IsSuccessStatusCode)
                        {
                            var json = await resp.Content.ReadAsStringAsync();
                            var p = JsonConvert.DeserializeObject<ApiPlayerDto>(json);
                            if (p != null)
                            {
                                _playerName = p.FirstName ?? "";
                                _playerPhone = p.Phone ?? "";
                                _playerCountry = p.Country ?? "";
                            }
                        }
                    }
                    catch { /* ignore name errors */ }
                }

                await LoadExistingGameAsync(argGameId.Value);
                return;
            }

            // Otherwise, ask for PlayerId and start a fresh game
            _externalPlayerId = argPlayerId ?? PromptForExternalPlayerId();
            if (_externalPlayerId == 0)
            {
                MessageBox.Show("No valid PlayerId entered. Exiting.");
                Close();
                return;
            }

            // Optional: verify player exists on server and get name
            try
            {
                var resp = await _http.GetAsync($"player/{_externalPlayerId}");
                if (!resp.IsSuccessStatusCode)
                {
                    MessageBox.Show("Player not found on server. Please register first on the website.");
                    Close();
                    return;
                }
                var json = await resp.Content.ReadAsStringAsync();
                var p = JsonConvert.DeserializeObject<ApiPlayerDto>(json);
                _playerName = p?.FirstName ?? "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error getting player info: {ex.Message}");
                Close();
                return;
            }

            await StartNewGameAsync(_externalPlayerId);
        }

        private async Task LoadExistingGameAsync(int gameId)
        {
            try
            {
                var resp = await _http.GetAsync($"{gameId}");
                if (!resp.IsSuccessStatusCode)
                {
                    MessageBox.Show("Failed to load existing game from server.");
                    Close();
                    return;
                }

                var json = await resp.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<ApiGameStateResponse>(json);
                if (data == null || data.Board == null)
                {
                    MessageBox.Show("Failed to load existing game from server.");
                    Close();
                    return;
                }

                _currentGameId = data.GameId;
                _board = To2D(data.Board);
                _targetBoard = To2D(data.Board);
                _gameOver = data.Status == "player_won" || data.Status == "server_won" || data.Status == "draw";
                _isAnimating = false;
                _animQueue.Clear();
                _currentAnim = null;

                UpdateInfoLabel();
                Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error contacting server: {ex.Message}");
                Close();
            }
        }

        private async Task StartNewGameAsync(int externalPlayerId)
        {
            try
            {
                var body = new ApiStartGameRequest { PlayerId = externalPlayerId };
                var jsonBody = JsonConvert.SerializeObject(body);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var res = await _http.PostAsync("start", content);
                if (!res.IsSuccessStatusCode)
                {
                    MessageBox.Show($"Failed to start game: {res.StatusCode}");
                    return;
                }

                var json = await res.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<ApiStartGameResponse>(json);
                _currentGameId = data?.GameId;
                if (_currentGameId == null || _currentGameId <= 0)
                {
                    MessageBox.Show("Server did not return a valid GameId.");
                    return;
                }

                // Initialize boards from server state
                _board = To2D(data.Board);
                _targetBoard = To2D(data.Board);
                _gameOver = false;
                _isAnimating = false;
                _animQueue.Clear();
                _currentAnim = null;

                UpdateInfoLabel();
                Invalidate(); // redraw
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error contacting server: {ex.Message}");
            }
        }

        private void UpdateInfoLabel()
        {
            var gid = _currentGameId ?? 0;
            if (!string.IsNullOrWhiteSpace(_playerName))
            {
                _lblInfo.Text =
                    $"Player: {_playerName} (ID {_externalPlayerId}) | Phone: {_playerPhone} | Country: {_playerCountry} | Game ID: {gid}";
            }
            else
            {
                _lblInfo.Text = $"Game ID: {gid}";
            }
        }


        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            DrawBoard(e.Graphics);
        }

        private void DrawBoard(Graphics g)
        {
            // background
            g.FillRectangle(Brushes.White, new Rectangle(0, TopMargin, Cols * CellSize, Rows * CellSize));

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    int x = col * CellSize;
                    int y = TopMargin + row * CellSize;
                    var cell = new Rectangle(x, y, CellSize, CellSize);

                    // cell background
                    g.FillRectangle(Brushes.White, cell);
                    g.DrawRectangle(Pens.Black, cell);

                    // disc
                    if (_board[row, col] == 1)
                        g.FillEllipse(Brushes.Red, cell);
                    else if (_board[row, col] == 2)
                        g.FillEllipse(Brushes.Yellow, cell);
                }
            }
        }

        protected override async void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (_gameOver || _isAnimating) return;
            if (_currentGameId is null || _currentGameId <= 0) return;

            // Click must be inside the board area
            if (e.Y < TopMargin) return;

            int col = e.X / CellSize;
            if (col < 0 || col >= Cols) return;

            await SubmitMoveAsync(col);
        }

        private async Task SubmitMoveAsync(int col)
        {
            try
            {
                // Send the move to the server
                var req = new ApiMoveRequest { GameId = _currentGameId.Value, Column = col };
                var jsonBody = JsonConvert.SerializeObject(req);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var res = await _http.PostAsync("move", content);
                if (!res.IsSuccessStatusCode)
                {
                    MessageBox.Show($"Server rejected the move: {res.StatusCode}");
                    return;
                }

                var json = await res.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<ApiMoveResponse>(json);
                if (data == null || data.Board == null)
                {
                    MessageBox.Show("Invalid response from server.");
                    return;
                }

                // Convert returned board to 2D and animate differences
                _targetBoard = To2D(data.Board);
                PrepareAnimationsFromDiff(_board, _targetBoard);
                if (_animQueue.Count > 0)
                {
                    _isAnimating = true;
                    _currentAnim = _animQueue.Dequeue();
                    _currentAnim.CurrentRow = 0;
                    _dropTimer.Start();
                }
                else
                {
                    // No animation needed (should not happen), snap to final
                    _board = (int[,])_targetBoard.Clone();
                    Invalidate();
                }

                // Handle end-state after animation completes
                if (data.Status == "player_won" || data.Status == "server_won" || data.Status == "draw")
                    _pendingEndStatus = data.Status;
                else
                    _pendingEndStatus = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error contacting server: {ex.Message}");
            }
        }

        // Build animation queue by comparing old vs new boards.
        private void PrepareAnimationsFromDiff(int[,] oldB, int[,] newB)
        {
            _animQueue.Clear();

            (int row, int col)? playerDrop = null;
            (int row, int col)? serverDrop = null;

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    if (oldB[r, c] == 0 && newB[r, c] == 1) playerDrop = (r, c);
                    if (oldB[r, c] == 0 && newB[r, c] == 2) serverDrop = (r, c);
                }
            }

            if (playerDrop.HasValue)
            {
                _animQueue.Enqueue(new DropAnim
                {
                    Player = 1,
                    Col = playerDrop.Value.col,
                    TargetRow = playerDrop.Value.row,
                    CurrentRow = 0
                });
            }
            if (serverDrop.HasValue)
            {
                _animQueue.Enqueue(new DropAnim
                {
                    Player = 2,
                    Col = serverDrop.Value.col,
                    TargetRow = serverDrop.Value.row,
                    CurrentRow = 0
                });
            }
        }

        private void DropTimer_Tick(object sender, EventArgs e)
        {
            if (_currentAnim == null)
            {
                _dropTimer.Stop();
                _isAnimating = false;
                return;
            }

            // Clear previous falling position
            if (_currentAnim.CurrentRow > 0)
                _board[_currentAnim.CurrentRow - 1, _currentAnim.Col] = 0;

            // Set current falling position
            _board[_currentAnim.CurrentRow, _currentAnim.Col] = _currentAnim.Player;
            Invalidate();

            // Reached target?
            if (_currentAnim.CurrentRow >= _currentAnim.TargetRow)
            {
                _board[_currentAnim.TargetRow, _currentAnim.Col] = _currentAnim.Player;

                if (_animQueue.Count > 0)
                {
                    _currentAnim = _animQueue.Dequeue();
                    _currentAnim.CurrentRow = 0;
                }
                else
                {
                    _dropTimer.Stop();
                    _isAnimating = false;
                    _currentAnim = null;

                    // Snap board to server's official final state to avoid drift
                    _board = (int[,])_targetBoard.Clone();
                    Invalidate();

                    // If there is a pending end status, show it now
                    if (!string.IsNullOrEmpty(_pendingEndStatus))
                    {
                        if (_pendingEndStatus == "player_won")
                            MessageBox.Show("You win!", "Game Over");
                        else if (_pendingEndStatus == "server_won")
                            MessageBox.Show("Server wins!", "Game Over");
                        else if (_pendingEndStatus == "draw")
                            MessageBox.Show("It's a draw!", "Game Over");

                        _gameOver = true;
                        _pendingEndStatus = null;
                    }
                }
            }
            else
            {
                _currentAnim.CurrentRow++;
            }
        }
    }
}
