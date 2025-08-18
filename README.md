# Connect4 – ASP.NET Core + WinForms 🎮🖥️

A complete **Connect 4** solution with:

* **ASP.NET Core** Razor Pages site + **Web API** server ⚙️
* **WinForms** client with **timer-based animation** ⏱️
* **Central SQL Server** DB (server) + **LocalDB** (client) for **record/replay** 🗄️🔁

Built to satisfy the course requirements: registration, gameplay vs server (random opponent), queries, updates/deletes, local recording, and replay. ✅

---

## 🧭 Table of Contents

* [🏗️ Architecture](#️-architecture)
* [🗂️ Project Structure](#️-project-structure)
* [⭐ Key Features (by requirement)](#-key-features-by-requirement)
* [📦 Prerequisites](#-prerequisites)
* [🚀 Server — Setup & Run](#-server--setup--run)
* [🎯 Client — Setup & Run](#-client--setup--run)
* [🕹️ Gameplay Flow](#️-gameplay-flow)
* [📋 Queries (Razor Pages)](#-queries-razor-pages)
* [🔗 API Endpoints](#-api-endpoints)
* [🗃️ Data Model](#️-data-model)
* [🔁 Replay & Local Recording](#-replay--local-recording)
* [⚙️ Configuration Notes](#️-configuration-notes)
* [📝 Notes for Reviewers](#-notes-for-reviewers)

---

## 🏗️ Architecture

**Server (Connect4\_Server)**
ASP.NET Core Razor Pages (+ Web API under `/api/GameApi/*`) • EF Core with SQL Server • Session-based login
Handles registration/login, starting games, validating moves, server random moves, win/draw detection, queries, updates, deletes.

**Client (Connect4\_Client)**
WinForms app • Renders a 6×7 board from a **rectangular matrix** (`int[6,7]`) 🧱
**Timer-based animation** for falling discs ⏱️ • Talks to Web API • Records games locally (LocalDB) & supports **replay** 🔁

---

## 🗂️ Project Structure

```
Connect4_DotNet_Project/
├─ Connect4_Server/              # ASP.NET Core (Razor Pages + Web API)
│  ├─ Data/AppDbContext.cs
│  ├─ Models/Player.cs, Game.cs, Move.cs, GameResult.cs
│  ├─ Controllers/GameApiController.cs
│  ├─ Pages/ (Index, Register, Login, NewGame, Queries, EditPlayer, About, _Layout, ...)
│  ├─ appsettings.json
│  └─ Program.cs
└─ Connect4_Client/              # WinForms client
   ├─ Program.cs
   ├─ Form1.cs (+ Designer)
   ├─ LocalRecorder.cs           # LocalDB (recorded games & moves)
   └─ DTOs (GameStateDto, PlayerDto)
```

---

## ⭐ Key Features (by requirement)

* 🧍 **Registration page** with validation & country combo (Razor Pages)
* 🌐 **Web API** for gameplay (`/api/GameApi/...`)
* 🤖 **Server vs Player**: server picks random **legal** moves
* 🧠 **Game rules enforced**: turns, win (4-in-a-row), draw
* 🗄️ **Central DB** (players, games, moves)
* 📋 **Queries page** implements the assignment’s query set (17–24)
* ✏️ **Update/Delete**: edit player (with rules), delete game/player
* 🧱 **Client board uses rectangular matrix only**
* 🎞️ **Animation**: falling disc via `Timer`
* 🔴🟡 **Local recording** per game (client LocalDB) + **replay** with equal time gaps

---

## 📦 Prerequisites

* .NET 6+
* SQL Server (server DB)
* SQL Server **LocalDB** (client recording) — `Data Source=(LocalDB)\MSSQLLocalDB`

> Connection strings are configurable; see **Configuration Notes**.

---

## 🚀 Server — Setup & Run

```bash
dotnet restore
dotnet run
```

Set `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=Connect4ServerDb;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

Dev URL (typical): **[https://localhost:7150/](https://localhost:7150/)** 🔒

---

## 🎯 Client — Setup & Run

* **Auto-launch from website** (recommended): New Game → launches client with args.
* **Existing game:**

  ```bash
  Connect4_Client.exe --gameId=123 --playerId=45 --api="https://localhost:7150/api/GameApi/"
  ```
* **Replay recorded game:**

  ```bash
  Connect4_Client.exe --replayServerGameId=123 --playerId=45 --api="https://localhost:7150/api/GameApi/"
  ```

No args? The client prompts for **external PlayerId (1..1000)**.

---

## 🕹️ Gameplay Flow

1. 🧍 Register (website)
2. 🔑 Login (website)
3. 🎮 Start New Game (website) → launches WinForms client
4. 🖱️ Click a column to drop a disc → server validates & responds
5. 🎞️ Discs **fall** with `Timer` animation; moves are **recorded locally**
6. 🏁 End state updates local result & duration

---

## 📋 Queries (Razor Pages)

* 👥 All players + sorting (case-insensitive asc / case-sensitive desc)
* 🕒 Last game date per player (case-sensitive name sort, desc; **two columns**)
* 🎮 All games with details
* 🎯 Distinct games (one per player)
* 🔢 Games per player (counts)
* 📊 Group by number of games (desc)
* 🌍 Group by country

Also includes: ✏️ Edit Player (locks external `PlayerId` after games), 🗑️ Delete Game/Player.

---

## 🔗 API Endpoints

Base: `https://localhost:7150/api/GameApi/`

* `GET player/{playerId:int}` → player by **external** ID
* `GET current` → session current player
* `POST start` → `{ "PlayerId": <externalId> }` → new game state
* `POST move` → `{ "GameId": <id>, "Column": 0..6 }` → updated board/status
* `GET {gameId:int}` → full state (board + status + move list)

🔁 Server sends `int[][]`; client converts to `int[,]`.

---

## 🗃️ Data Model

**Server (EF Core)**

* **Player**: `Id`, `PlayerId (1..1000 unique)`, `FirstName`, `Phone`, `Country`, `Games`
* **Game**: `Id`, `StartTime (UTC)`, `Duration`, `Moves (CSV)`, `Result`, `PlayerId (FK)`, `MoveList`
* **Move**: `Id`, `GameId (FK)`, `MoveNumber (1..n)`, `Column (0..6)`, `IsPlayerMove`

**Client (LocalDB)**

* **LocalGames**: `LocalGameId (PK)`, `ServerGameId (UNIQUE)`, `PlayerExternalId`, `PlayerName`, `StartedAtUtc`, `DurationSeconds?`, `Result`
* **LocalMoves**: `Id (PK)`, `LocalGameId (FK, CASCADE)`, `TurnIndex (unique per game)`, `Column`, `PlayerType`

---

## 🔁 Replay & Local Recording

* On new/loaded game: create/ensure local row 🗄️
* Each turn appends **human** then **server** move (incremental `TurnIndex`)
* On end: set **Result** + **DurationSeconds**
* **Replay**: `--replayServerGameId=X` → fixed-interval playback via `Timer` ⏱️

---

## ⚙️ Configuration Notes

* **Server DB**: `ConnectionStrings:DefaultConnection` in `appsettings.json`.
* **Client EXE path (website launch)** — add to **Connect4\_Server/appsettings.json**:

  ```json
  { "ClientExePath": "C:\\Path\\To\\Connect4_Client\\bin\\Debug\\Connect4_Client.exe" }
  ```
* **API base**: default `https://localhost:7150/api/GameApi/`; override with `--api="..."`.

---

## 📝 Notes for Reviewers

* 🧱 Matrix constraint respected on client (`int[6,7]`)
* ⏱️ Creative animation implemented with `Timer`
* ✅ DataAnnotations with custom messages
* 🔒 External `PlayerId (1..1000)` enforced unique
* 🧰 Session convention: site stores **internal** DB `Id` as `CurrentPlayerId`

---

Happy connecting 4! ❤️🟡
