using Microsoft.EntityFrameworkCore;
using MyApi.Models;

namespace MyApi.Data
{
    public class BooksDbContext : DbContext
    {
        public BooksDbContext(DbContextOptions<BooksDbContext> options) : base(options) { }

        public DbSet<FavoriteBook> FavoriteBooks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // No jsonb needed now — AuthorsJson is just string
        }
        public DbSet<SearchHistory> SearchHistories { get; set; }

    }
}
