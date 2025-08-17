using Microsoft.EntityFrameworkCore;
using Connect4_Server.Models;

namespace Connect4_Server.Data
{
    public class AppDbContext: DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Move> Moves { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<Game> Games { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Ensure external PlayerId (1..1000) is globally unique
            modelBuilder.Entity<Player>()
                .HasIndex(p => p.PlayerId)
                .IsUnique();
        }

    }
}
