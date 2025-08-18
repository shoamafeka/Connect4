using Microsoft.EntityFrameworkCore;
using Connect4_Server.Models;

namespace Connect4_Server.Data
{
    // AppDbContext: central EF Core context for the server-side DB (players, games, moves).
    // Configured via DI with provider/connection string in Program/Startup.
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // DbSets (tables)
        // Moves: every recorded step in a game (usually references Game and optionally Player).
        public DbSet<Move> Moves { get; set; } = default!;

        // Players: registered users. External PlayerId (1..1000) must be unique per project requirements.
        public DbSet<Player> Players { get; set; } = default!;

        // Games: played sessions on the server. Each references the initiating Player and metadata (start/end).
        public DbSet<Game> Games { get; set; } = default!;

        // Fluent model configuration for indexes/constraints; relationships rely on EF conventions unless specified.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Keep base call for future EF changes (safe no-op today).
            base.OnModelCreating(modelBuilder);

            // Enforce global uniqueness of the *external* PlayerId (chosen by the user, 1..1000).
            // Matches the site rule: an ID already in use cannot be registered again.
            modelBuilder.Entity<Player>()
                .HasIndex(p => p.PlayerId)
                .IsUnique();
        }
    }
}
