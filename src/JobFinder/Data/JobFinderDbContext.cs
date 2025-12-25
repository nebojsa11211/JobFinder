using System.IO;
using Microsoft.EntityFrameworkCore;
using JobFinder.Models;

namespace JobFinder.Data;

public class JobFinderDbContext : DbContext
{
    public DbSet<Job> Jobs => Set<Job>();

    public string DbPath { get; }

    public JobFinderDbContext()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(folder, "JobFinder");
        Directory.CreateDirectory(appFolder);
        DbPath = Path.Combine(appFolder, "jobfinder.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.LinkedInJobId).IsUnique();
            entity.Property(e => e.Status).HasConversion<string>();
        });
    }
}
