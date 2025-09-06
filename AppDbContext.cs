using Microsoft.EntityFrameworkCore;
using MyProfessionalss.Data.Model;

namespace MyProfessionals.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<CompanyAdmin> CompanyAdmins { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure CompanyAdmin entity
            modelBuilder.Entity<CompanyAdmin>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Ensure a user cannot be assigned multiple times to the same company
                entity.HasIndex(e => new { e.CompanyId, e.UserId })
                      .IsUnique();

                entity.Property(e => e.AssignedDate)
                      .IsRequired();

                entity.HasOne(e => e.Company)
                      .WithMany(c => c.Admins)
                      .HasForeignKey(e => e.CompanyId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
