using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;



namespace Connect4_Client
{
    

    public partial class Form1 : Form
    {



        private bool isPlayerTurn = true; // true = Red (Player), false = Yellow (Server)
        private Random random = new Random();

        private Timer dropTimer = new Timer();
        private int animCol = -1;
        private int animRow = 0;
        private int targetRow = -1;
        private Color animColor = Color.Red;

        private bool isAnimating = false;
        //private Button[,] boardButtons = new Button[6, 7];
        private int[,] boardState = new int[rows, cols]; // 0 = empty, 1 = player, 2 = server
        private int currentPlayer = 1;

        private const int rows = 6;
        private const int cols = 7;
        private const int cellSize = 60;



        // Player information
        private PlayerDto currentPlayerInfo;
        private Label lblPlayerInfo;



        public Form1()
        {
            InitializeComponent();
        }


        private async Task<PlayerDto> GetPlayerFromServer(int playerId)
        {
            using (HttpClient client = new HttpClient())
            {
                // Point to your API root
                client.BaseAddress = new Uri("https://localhost:7150/api/GameApi/");

                try
                {
                    // This calls: https://localhost:7150/api/GameApi/player/{id}
                    var response = await client.GetAsync($"player/{playerId}");

                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadFromJsonAsync<PlayerDto>();
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        MessageBox.Show("Player not found.");
                        return null;
                    }
                    else
                    {
                        MessageBox.Show($"Server returned error: {response.StatusCode}");
                        return null;
                    }
                }
                catch (HttpRequestException ex)
                {
                    MessageBox.Show($"Error contacting server: {ex.Message}");
                    return null;
                }
            }
        }




        private async void Form1_Load(object sender, EventArgs e)
        {
            this.DoubleBuffered = true; // smoother graphics

            this.ClientSize = new Size(cols * cellSize, rows * cellSize);
            lblPlayerInfo = new Label();
            lblPlayerInfo.AutoSize = true;
            lblPlayerInfo.Font = new Font("Arial", 12, FontStyle.Bold);
            lblPlayerInfo.ForeColor = Color.Black;
            lblPlayerInfo.Location = new Point(10, 10);
            this.Controls.Add(lblPlayerInfo);

            dropTimer.Interval = 50; // speed of the drop
            dropTimer.Tick += DropTimer_Tick;

            var player = await GetPlayerFromServer(111); // example: playerId = 111- DonelMan
            if (player != null)
            {
                currentPlayerInfo = player;
                lblPlayerInfo.Text = $"Player: {player.FirstName} | Phone: {player.Phone} | Country: {player.Country}";
            }
            else
            {
                lblPlayerInfo.Text = "Player not found.";
            }

        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
           
            //if (currentPlayerInfo != null)
            //{
            //    e.Graphics.DrawString(
            //        $"Player: {currentPlayerInfo.FirstName} ({currentPlayerInfo.Country})",
            //        new Font("Arial", 12, FontStyle.Bold),
            //        Brushes.Black,
            //        10,
            //        10
            //    );
            //}

            DrawBoard(e.Graphics);

        }

        private void DrawBoard(Graphics g)
        {
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int x = col * cellSize;
                    int y = row * cellSize;

                    Rectangle cell = new Rectangle(x, y, cellSize, cellSize);
                    g.FillRectangle(Brushes.White, cell);
                    g.DrawRectangle(Pens.Black, cell);

                    if (boardState[row, col] == 1)
                        g.FillEllipse(Brushes.Red, cell);
                    else if (boardState[row, col] == 2)
                        g.FillEllipse(Brushes.Yellow, cell);
                }
            }
        }


        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (!isPlayerTurn || isAnimating) return;
            int col = e.X / cellSize;
            if (col < 0 || col >= cols) return;

            if (StartDiscDrop(col, 1)){ 
            }
            else { 
                MessageBox.Show("Column is full! Choose another column.", "Invalid Move");
            }
        }



        private bool StartDiscDrop(int col, int player)
        {
            for (int row = rows - 1; row >= 0; row--)
            {
                if (boardState[row, col] == 0)
                {
                    animCol = col;
                    targetRow = row;
                    animRow = 0;
                    currentPlayer = player;
                    isAnimating = true;
                    dropTimer.Start();
                    return true;
                }
            }
            return false; // Column is full


        }


        private void ServerMove()
        {
            List<int> legalCols = new List<int>();
            for (int col = 0; col < cols; col++)
            {
                if (boardState[0, col] == 0)
                    legalCols.Add(col);
            }

            if (legalCols.Count == 0) return; // Full board = draw already handled

            int attempts = 0;
            while (attempts < 10)
            {
                int chosenCol = legalCols[random.Next(legalCols.Count)];

                if (StartDiscDrop(chosenCol, 2)) // 2 = Server
                    break;

                attempts++;
            }
        }


        private void DropTimer_Tick(object sender, EventArgs e)
        {
            if (animRow > 0)
                boardState[animRow - 1, animCol] = 0;

            boardState[animRow, animCol] = currentPlayer;
            Invalidate(); // triggers OnPaint

            if (animRow == targetRow)
            {
                dropTimer.Stop();

                if (CheckWin(animRow, animCol, currentPlayer))
                {
                    string winner = currentPlayer == 1 ? "Player" : "Server";
                    MessageBox.Show($"{winner} wins!", "Game Over");

                    isPlayerTurn = false; // disable further play
                    return; // stop here, don’t continue
                }


                bool isDraw = true;
                for (int c = 0; c < cols; c++)
                {
                    if (boardState[0, c] == 0)
                    {
                        isDraw = false;
                        break;
                    }
                }

                if (isDraw)
                {
                    MessageBox.Show("It's a draw!", "Game Over");
                    isPlayerTurn = false;
                    return;
                }



                if (currentPlayer == 1)
                {
                    isPlayerTurn = false;
                    Task.Delay(500).ContinueWith(_ =>
                    {
                        this.Invoke(new Action(ServerMove));
                    });
                }
                else
                {
                    isPlayerTurn = true;
                }

                isAnimating = false;

            }

            else
            {
                animRow++;
            }
        }

        private bool CheckWin(int row, int col, int player)
        {
            return CheckDirection(row, col, player, 1, 0) || // Vertical
                   CheckDirection(row, col, player, 0, 1) || // Horizontal
                   CheckDirection(row, col, player, 1, 1) || // Diagonal down-right
                   CheckDirection(row, col, player, 1, -1);  // Diagonal up-right
        }

        private bool CheckDirection(int row, int col, int player, int deltaRow, int deltaCol)
        {
            int count = 1;

            // Check in the positive direction
            count += CountInDirection(row, col, player, deltaRow, deltaCol);

            // Check in the negative direction
            count += CountInDirection(row, col, player, -deltaRow, -deltaCol);

            return count >= 4;
        }

        private int CountInDirection(int row, int col, int player, int dRow, int dCol)
        {
            int count = 0;
            int r = row + dRow;
            int c = col + dCol;

            while (r >= 0 && r < rows && c >= 0 && c < cols && boardState[r, c] == player)
            {
                count++;
                r += dRow;
                c += dCol;
            }

            return count;
        }





    }
}
