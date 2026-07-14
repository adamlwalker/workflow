using Microsoft.EntityFrameworkCore;
using PDFProofer.Core.Models;

namespace PDFProofer.Core.Data;

/// <summary>
/// Database context for the PDF Proofer application (CON-T-001)
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }
    
    public DbSet<Job> Jobs { get; set; }
    public DbSet<Destination> Destinations { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure Job entity
        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(j => j.Id);
            entity.Property(j => j.JobNumber).IsRequired();
            entity.Property(j => j.FilenameStem).IsRequired();
            entity.Property(j => j.State).HasConversion<string>(); // Store as string for SQLite
        });
        
        // Configure Destination entity
        modelBuilder.Entity<Destination>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.DestinationId).IsRequired();
        });
    }
}