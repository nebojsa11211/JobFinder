using JobFinder.Models;

namespace JobFinder.Services;

/// <summary>
/// Upwork-specific job platform service.
/// Extends the generic IJobPlatformService with Upwork-specific operations.
/// </summary>
public interface IUpworkService : IJobPlatformService
{
    /// <summary>
    /// Gets the current Connects balance (Upwork's credit system for applying to jobs).
    /// </summary>
    /// <returns>Number of available Connects, or null if unable to retrieve.</returns>
    Task<int?> GetConnectsBalanceAsync();

    /// <summary>
    /// Checks if user has enough Connects to apply for a specific job.
    /// </summary>
    /// <param name="connectsRequired">Number of Connects required for the job.</param>
    /// <returns>True if user has enough Connects.</returns>
    Task<bool> HasEnoughConnectsAsync(int connectsRequired);
}

/// <summary>
/// Upwork-specific search filter extensions.
/// </summary>
public class UpworkSearchFilter
{
    /// <summary>
    /// Base search filter with common parameters.
    /// </summary>
    public SearchFilter BaseFilter { get; set; } = new();

    /// <summary>
    /// Job category (e.g., "Web Development", "Mobile Development").
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Subcategory within the main category.
    /// </summary>
    public string? Subcategory { get; set; }

    /// <summary>
    /// Budget type filter: null = all, "hourly" = hourly only, "fixed" = fixed price only.
    /// </summary>
    public string? BudgetType { get; set; }

    /// <summary>
    /// Minimum hourly rate filter.
    /// </summary>
    public decimal? MinHourlyRate { get; set; }

    /// <summary>
    /// Maximum hourly rate filter.
    /// </summary>
    public decimal? MaxHourlyRate { get; set; }

    /// <summary>
    /// Minimum fixed price budget filter.
    /// </summary>
    public decimal? MinFixedPrice { get; set; }

    /// <summary>
    /// Maximum fixed price budget filter.
    /// </summary>
    public decimal? MaxFixedPrice { get; set; }

    /// <summary>
    /// Client history filter: "no_hires" = new clients, "1_plus" = 1+ hires, "10_plus" = 10+ hires.
    /// </summary>
    public string? ClientHistory { get; set; }

    /// <summary>
    /// Project length filter: "week" = less than a week, "month" = 1-3 months, "semester" = 3-6 months, "ongoing" = ongoing.
    /// </summary>
    public string? ProjectLength { get; set; }

    /// <summary>
    /// Hours per week filter: "part_time" = less than 30 hrs/week, "full_time" = 30+ hrs/week.
    /// </summary>
    public string? HoursPerWeek { get; set; }

    /// <summary>
    /// Experience level filter: "entry" = entry level, "intermediate" = intermediate, "expert" = expert.
    /// </summary>
    public string? ExperienceLevel { get; set; }

    /// <summary>
    /// Number of proposals filter: "0-5", "5-10", "10-15", "15-20", "20-50".
    /// </summary>
    public string? ProposalsRange { get; set; }

    /// <summary>
    /// Payment verification filter: true = only verified payment methods.
    /// </summary>
    public bool? PaymentVerified { get; set; }

    /// <summary>
    /// Sort order: "recency" = newest first, "relevance" = most relevant.
    /// </summary>
    public string SortBy { get; set; } = "recency";
}
