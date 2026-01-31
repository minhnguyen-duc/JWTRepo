using Microsoft.EntityFrameworkCore;
using JwtAuthDemo.Models;

namespace JwtAuthDemo.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =====================================================================
            // User Configuration
            // =====================================================================
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Password).IsRequired();
                entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
                entity.Property(e => e.CreatedAt).IsRequired();
                // KHÔNG CÓ HasDefaultValue - giá trị được set trong Model class
            });

            // =====================================================================
            // RefreshToken Configuration
            // =====================================================================
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Token).IsUnique();
                entity.Property(e => e.Token).IsRequired();
                entity.Property(e => e.ExpiryDate).IsRequired();
                entity.Property(e => e.Created).IsRequired();
                entity.Property(e => e.IsRevoked).HasDefaultValue(false);
                // KHÔNG CÓ HasDefaultValue cho Created

                // Relationship
                entity.HasOne(e => e.User)
                      .WithMany(u => u.RefreshTokens)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Seed data
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Password đã hash sẵn cho "password123"
            // Dùng giá trị cố định để tránh hash lại mỗi lần build
            var hashedPassword = "$2a$11$XGUuaa2ZCSRQ7cguYfFrwO1jpCN2t1jxyPqF4QZfnmbYwUp6C9W9.";
            
            // Fixed date - KHÔNG DÙNG DateTime.UtcNow
            var seedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    Password = hashedPassword,
                    Role = "Admin",
                    CreatedAt = seedDate
                },
                new User
                {
                    Id = 2,
                    Username = "user",
                    Password = hashedPassword,
                    Role = "User",
                    CreatedAt = seedDate
                }
            );
        }
    }
}