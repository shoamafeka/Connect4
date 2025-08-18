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
        
        // --- Local recording ---
        private LocalRecorder _rec;
        private int _localGameId = -1;     // local id in LocalGames
        private int _turnIndex = 0;        // 0..n
        private DateTime _gameStartUtc;    // for duration

        // --- Replay state ---
        private Timer _replayTimer;
        private List<MoveRecord> _replayMoves;
        private int _replayIndex;
        private bool _isReplayMode = false;


        // Board snapshot used only to detect server column from diff
        private int[,] _beforeBoard = new int[6, 7]; // adjust to your Rows/Cols if different



        public Form1()
        {
            InitializeComponent();
            _rec = new LocalRecorder();
        
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

        // Parse command-line args: --gameId=123 --playerId=45 --api="https://.../api/GameApi/" --replayServerGameId=123
        private (int? gameId, int? playerId, string api, int? replayServerGameId) ParseArgs()
        {
            int? gameId = null, playerId = null, replay = null;
            string api = null;

            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("--gameId=", StringComparison.OrdinalIgnoreCase))
                {
                    var val = arg.Substring("--gameId=".Length).Trim('"');
                    if (int.TryParse(val, out int g)) gameId = g;
                }
                else if (arg.StartsWith("--replayServerGameId=", StringComparison.OrdinalIgnoreCase))
                {
                    var val = arg.Substring("--replayServerGameId=".Length).Trim('"');
                    if (int.TryParse(val, out int r)) replay = r;
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
            return (gameId, playerId, api, replay);
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
            var (argGameId, argPlayerId, argApi, argReplayServerGameId) = ParseArgs();
            if (!string.IsNullOrWhiteSpace(argApi))
            {
                _apiBase = argApi.Trim();
                if (!_apiBase.EndsWith("/")) _apiBase += "/";
            }
            _http = new HttpClient { BaseAddress = new Uri(_apiBase) };


            // --- DIRECT REPLAY MODE (runs before any new/existing game flow) ---
            if (argReplayServerGameId.HasValue)
            {
                // Optional: populate label with player info
                if (argPlayerId.HasValue && argPlayerId.Value > 0)
                {
                    _externalPlayerId = argPlayerId.Value;
                    try
                    {
                        var resp = await _http.GetAsync($"player/{_externalPlayerId}");
                        if (resp.IsSuccessStatusCode)
                        {
                            var jsonP = await resp.Content.ReadAsStringAsync();
                            var p = JsonConvert.DeserializeObject<ApiPlayerDto>(jsonP);
                            if (p != null)
                            {
                                _playerName = p.FirstName ?? "";
                                _playerPhone = p.Phone ?? "";
                                _playerCountry = p.Country ?? "";
                            }
                        }
                    }
                    catch { /* ignore */ }
                }

                // Look up the local recording by ServerGameId and start replay
                var localId = _rec.FindLocalGameIdByServerGameId(argReplayServerGameId.Value);
                if (localId.HasValue)
                {
                    StartReplay(localId.Value);
                    return; // IMPORTANT: stop normal flow
                }
                else
                {
                    MessageBox.Show("This game is not recorded locally on this machine.", "Replay");
                    Close();
                    return;
                }
            }



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

                // >>> add this <<<
                if (!_gameOver)
                {
                    if (_externalPlayerId == 0) _externalPlayerId = _externalPlayerId;
                    StartLocalRecording(_currentGameId.Value, _externalPlayerId, _playerName);
                }


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

                // >>> START LOCAL RECORDING  <<<
                StartLocalRecording(_currentGameId.Value, _externalPlayerId, _playerName);

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

            if (_isReplayMode) return;

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
                // Snapshot board BEFORE sending the move (for server column detection later)
                CopyBoard(_board, _beforeBoard);


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

                // --- Recording: human move + server move ---
                // Add human move (only after server accepted it)
                if (_localGameId > 0)
                    _rec.AddMove(_localGameId, _turnIndex++, col, 1); // 1 = Human

                // Detect server column from the diff between BEFORE (snapshot) and target from server
                int serverColDetected = DetectServerColumnFromDiff(_beforeBoard, _targetBoard);
                if (_localGameId > 0 && serverColDetected >= 0)
                    _rec.AddMove(_localGameId, _turnIndex++, serverColDetected, 2); // 2 = Server

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


                        // --- Recording: finalize game row with result/duration ---
                        if (!string.IsNullOrEmpty(_pendingEndStatus) && _localGameId > 0)
                        {
                            byte result = 0; // 0=Unknown, 1=HumanWin, 2=ServerWin, 3=Draw
                            if (_pendingEndStatus == "player_won") result = 1;
                            else if (_pendingEndStatus == "server_won") result = 2;
                            else if (_pendingEndStatus == "draw") result = 3;

                            int durationSeconds = (int)(DateTime.UtcNow - _gameStartUtc).TotalSeconds;
                            _rec.FinishGame(_localGameId, result, durationSeconds);
                        }

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

         //--- Recording: call this right after you get the server GameId ---
        private void StartLocalRecording(int serverGameId, int playerExternalId, string playerName)
        {
            // Store start time and open (or create) a local game row
            _gameStartUtc = DateTime.UtcNow;
            _localGameId = _rec.EnsureLocalGame(serverGameId, playerExternalId, playerName, _gameStartUtc);
            _turnIndex = 0; // first move will be turnIndex 0
        }

        // Deep-copy a 2D board (source -> destination)
        private void CopyBoard(int[,] src, int[,] dst)
        {
            int rows = src.GetLength(0);
            int cols = src.GetLength(1);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    dst[r, c] = src[r, c];
        }

        // Find the column where the server dropped a '2' between two board states
        private int DetectServerColumnFromDiff(int[,] before, int[,] after)
        {
            int rows = before.GetLength(0);
            int cols = before.GetLength(1);

            for (int c = 0; c < cols; c++)
            {
                for (int r = rows - 1; r >= 0; r--)
                {
                    if (before[r, c] != after[r, c])
                    {
                        if (after[r, c] == 2 && before[r, c] == 0)
                            return c; // server disc appeared here
                        break;
                    }
                }
            }
            return -1; // not found yet (e.g., player already won and server didn't move)
        }

        private int GetDropRow(int[,] b, int col)
        {
            for (int r = Rows - 1; r >= 0; r--)
                if (b[r, col] == 0) return r;
            return -1;
        }

        private void ResetBoardToEmpty()
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    _board[r, c] = 0;
                    _targetBoard[r, c] = 0;
                }
            _gameOver = false;
            _isAnimating = false;
            _animQueue.Clear();
            _currentAnim = null;
            Invalidate();
        }

        private void StartReplay(int localGameId)
        {
            _isReplayMode = true;
            ResetBoardToEmpty();

            _replayMoves = _rec.LoadMoves(localGameId);
            _replayIndex = 0;

            if (_replayTimer == null)
            {
                _replayTimer = new Timer();
                _replayTimer.Interval = 600; // fixed interval per requirement
                _replayTimer.Tick += ReplayTimer_Tick;
            }
            _replayTimer.Stop();
            _replayTimer.Start();

            _lblInfo.Text = "Replay mode (local recording)";
        }

        private void ReplayTimer_Tick(object sender, EventArgs e)
        {
            // wait until current animation finishes
            if (_isAnimating) return;

            if (_replayMoves == null || _replayIndex >= _replayMoves.Count)
            {
                _replayTimer.Stop();
                _isReplayMode = false;
                _lblInfo.Text = "Replay finished.";
                return;
            }

            var mv = _replayMoves[_replayIndex++];

            // build next target state for this single drop
            int row = GetDropRow(_targetBoard, mv.Column);
            if (row < 0) return; // defensive: column full
            _targetBoard[row, mv.Column] = mv.PlayerType;

            // animate via existing pipeline
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
                _board = (int[,])_targetBoard.Clone();
                Invalidate();
            }
        }



    }
}
