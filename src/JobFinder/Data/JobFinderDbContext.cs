using System.IO;
using Microsoft.EntityFrameworkCore;
using JobFinder.Models;

namespace JobFinder.Data;

public class JobFinderDbContext : DbContext
{
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Company> Companies => Set<Company>();

    public string DbPath { get; }

    public JobFinderDbContext()
    {
        DbPath = Path.Combine(Environment.CurrentDirectory, "jobfinder.db");
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

            // Relationship to Company
            entity.HasOne(e => e.Company)
                .WithMany(c => c.Jobs)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.LinkedInId).IsUnique();
        });
    }

    /// <summary>
    /// Applies schema migrations for new columns added after initial release.
    /// </summary>
    public async Task MigrateSchemaAsync()
    {
        await Database.EnsureCreatedAsync();

        // Add missing columns if they don't exist (for existing databases)
        var connection = Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            // Add new columns for Jobs table
            await AddColumnIfNotExistsAsync(connection, "Jobs", "SummaryCroatian", "TEXT NULL");
            await AddColumnIfNotExistsAsync(connection, "Jobs", "ShortSummary", "TEXT NULL");
            await AddColumnIfNotExistsAsync(connection, "Jobs", "IsDiscarded", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfNotExistsAsync(connection, "Jobs", "Rating", "INTEGER NULL");
            await AddColumnIfNotExistsAsync(connection, "Jobs", "DiscardReason", "TEXT NULL");
            await AddColumnIfNotExistsAsync(connection, "Jobs", "CompanyId", "INTEGER NOT NULL DEFAULT 0");

            // Create Companies table if it doesn't exist
            await CreateCompaniesTableIfNotExistsAsync(connection);

            // Populate Companies from existing Jobs and link them
            await PopulateCompaniesFromJobsAsync(connection);

            // Try to drop legacy CompanyName/Company column (SQLite 3.35.0+)
            await TryDropColumnAsync(connection, "Jobs", "CompanyName");
            await TryDropColumnAsync(connection, "Jobs", "Company");
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task TryDropColumnAsync(
        System.Data.Common.DbConnection connection,
        string tableName,
        string columnName)
    {
        try
        {
            using var command = connection.CreateCommand();
            // Check if column exists
            command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name='{columnName}'";
            var exists = Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;

            if (exists)
            {
                command.CommandText = $"ALTER TABLE {tableName} DROP COLUMN {columnName}";
                await command.ExecuteNonQueryAsync();
            }
        }
        catch
        {
            // SQLite versions before 3.35.0 don't support DROP COLUMN - ignore error
        }
    }

    private static async Task PopulateCompaniesFromJobsAsync(System.Data.Common.DbConnection connection)
    {
        using var command = connection.CreateCommand();

        // Determine which column name exists (Company or CompanyName)
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Jobs') WHERE name='CompanyName'";
        var hasCompanyName = Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;

        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Jobs') WHERE name='Company'";
        var hasCompany = Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;

        string companyColumn;
        if (hasCompanyName)
            companyColumn = "CompanyName";
        else if (hasCompany)
            companyColumn = "Company";
        else
            return; // No company column exists, nothing to migrate

        // Find all unique company names from jobs that don't have a CompanyId yet
        command.CommandText = $@"
            SELECT DISTINCT {companyColumn}, Location
            FROM Jobs
            WHERE (CompanyId IS NULL OR CompanyId = 0) AND {companyColumn} IS NOT NULL AND {companyColumn} != ''";

        var companiesToCreate = new List<(string Name, string? Location, string ColumnName)>();

        using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                var location = reader.IsDBNull(1) ? null : reader.GetString(1);
                companiesToCreate.Add((name, location, companyColumn));
            }
        }

        // Create companies and update jobs
        foreach (var (name, location, colName) in companiesToCreate)
        {
            // Check if company already exists
            command.CommandText = "SELECT Id FROM Companies WHERE Name = @name";
            command.Parameters.Clear();
            var nameParam = command.CreateParameter();
            nameParam.ParameterName = "@name";
            nameParam.Value = name;
            command.Parameters.Add(nameParam);

            var existingId = await command.ExecuteScalarAsync();

            int companyId;
            if (existingId != null)
            {
                companyId = Convert.ToInt32(existingId);
            }
            else
            {
                // Insert new company
                command.CommandText = @"
                    INSERT INTO Companies (Name, Headquarters, DateAdded, IsBlacklisted, IsFavorite)
                    VALUES (@name, @location, @dateAdded, 0, 0);
                    SELECT last_insert_rowid();";

                command.Parameters.Clear();

                nameParam = command.CreateParameter();
                nameParam.ParameterName = "@name";
                nameParam.Value = name;
                command.Parameters.Add(nameParam);

                var locationParam = command.CreateParameter();
                locationParam.ParameterName = "@location";
                locationParam.Value = (object?)location ?? DBNull.Value;
                command.Parameters.Add(locationParam);

                var dateParam = command.CreateParameter();
                dateParam.ParameterName = "@dateAdded";
                dateParam.Value = DateTime.Now.ToString("o");
                command.Parameters.Add(dateParam);

                companyId = Convert.ToInt32(await command.ExecuteScalarAsync());
            }

            // Update jobs with this company name to link to the company
            command.CommandText = $"UPDATE Jobs SET CompanyId = @companyId WHERE {colName} = @name AND (CompanyId IS NULL OR CompanyId = 0)";
            command.Parameters.Clear();

            var companyIdParam = command.CreateParameter();
            companyIdParam.ParameterName = "@companyId";
            companyIdParam.Value = companyId;
            command.Parameters.Add(companyIdParam);

            nameParam = command.CreateParameter();
            nameParam.ParameterName = "@name";
            nameParam.Value = name;
            command.Parameters.Add(nameParam);

            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task AddColumnIfNotExistsAsync(
        System.Data.Common.DbConnection connection,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name='{columnName}'";
        var exists = Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;

        if (!exists)
        {
            command.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}";
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task CreateCompaniesTableIfNotExistsAsync(System.Data.Common.DbConnection connection)
    {
        using var command = connection.CreateCommand();

        // Check if table exists
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Companies'";
        var exists = Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;

        if (!exists)
        {
            command.CommandText = @"
                CREATE TABLE Companies (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    LinkedInId TEXT NULL,
                    LinkedInUrl TEXT NULL,
                    Website TEXT NULL,
                    Industry TEXT NULL,
                    Size TEXT NULL,
                    Headquarters TEXT NULL,
                    Description TEXT NULL,
                    LogoUrl TEXT NULL,
                    Notes TEXT NULL,
                    IsBlacklisted INTEGER NOT NULL DEFAULT 0,
                    IsFavorite INTEGER NOT NULL DEFAULT 0,
                    DateAdded TEXT NOT NULL,
                    DateUpdated TEXT NULL
                )";
            await command.ExecuteNonQueryAsync();

            // Create indexes
            command.CommandText = "CREATE INDEX IX_Companies_Name ON Companies(Name)";
            await command.ExecuteNonQueryAsync();

            command.CommandText = "CREATE UNIQUE INDEX IX_Companies_LinkedInId ON Companies(LinkedInId) WHERE LinkedInId IS NOT NULL";
            await command.ExecuteNonQueryAsync();
        }
    }
}
