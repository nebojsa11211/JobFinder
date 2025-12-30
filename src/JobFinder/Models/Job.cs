using System.ComponentModel.DataAnnotations.Schema;

namespace JobFinder.Models;

public class Job
{
    public int Id { get; set; }

    /// <summary>
    /// The platform this job was scraped from.
    /// </summary>
    public JobPlatform Platform { get; set; } = JobPlatform.LinkedIn;

    /// <summary>
    /// External job ID from the platform (LinkedIn job ID, Upwork job ID, etc.).
    /// Unique per platform.
    /// </summary>
    public required string ExternalJobId { get; set; }

    /// <summary>
    /// LinkedIn job ID (alias for ExternalJobId for backward compatibility).
    /// </summary>
    [NotMapped]
    [Obsolete("Use ExternalJobId instead")]
    public string LinkedInJobId
    {
        get => ExternalJobId;
        set => ExternalJobId = value;
    }

    public required string Title { get; set; }

    /// <summary>
    /// Foreign key to Company table.
    /// </summary>
    public int CompanyId { get; set; }

    /// <summary>
    /// Navigation property to Company.
    /// </summary>
    public Company Company { get; set; } = null!;

    /// <summary>
    /// Temporary property for passing company name during scraping (not stored in DB).
    /// </summary>
    [NotMapped]
    public string? ScrapedCompanyName { get; set; }

    public required string Location { get; set; }
    public string? Description { get; set; }
    public string? ExperienceLevel { get; set; }
    public string? WorkplaceType { get; set; }
    public string? JobUrl { get; set; }
    public string? ExternalApplyUrl { get; set; }
    public string? RecruiterEmail { get; set; }

    /// <summary>
    /// Whether the job supports quick apply (LinkedIn Easy Apply, Upwork direct proposal).
    /// </summary>
    public bool HasEasyApply { get; set; }

    public DateTime DatePosted { get; set; }
    public DateTime DateScraped { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.New;
    public DateTime? DateApplied { get; set; }
    public string? Notes { get; set; }
    public string? SummaryCroatian { get; set; }

    /// <summary>
    /// AI-generated short summary (few words about the job) displayed under title.
    /// </summary>
    public string? ShortSummary { get; set; }

    public bool IsDiscarded { get; set; }

    /// <summary>
    /// AI-assigned rating from 1-10 based on match criteria.
    /// </summary>
    public int? Rating { get; set; }

    /// <summary>
    /// Reason provided by AI if job was auto-discarded.
    /// </summary>
    public string? DiscardReason { get; set; }

    /// <summary>
    /// The prompt that was sent to AI for analysis (for debugging).
    /// </summary>
    public string? AiPromptSent { get; set; }

    /// <summary>
    /// The raw response received from AI (for debugging).
    /// </summary>
    public string? AiRawResponse { get; set; }

    // ============================================================
    // Upwork-specific fields (null for LinkedIn jobs)
    // ============================================================

    /// <summary>
    /// Budget type for Upwork jobs: "Hourly" or "Fixed".
    /// </summary>
    public string? BudgetType { get; set; }

    /// <summary>
    /// Minimum hourly rate for Upwork hourly jobs.
    /// </summary>
    public decimal? HourlyRateMin { get; set; }

    /// <summary>
    /// Maximum hourly rate for Upwork hourly jobs.
    /// </summary>
    public decimal? HourlyRateMax { get; set; }

    /// <summary>
    /// Fixed price budget for Upwork fixed-price jobs.
    /// </summary>
    public decimal? FixedPriceBudget { get; set; }

    /// <summary>
    /// Estimated project duration (e.g., "1-3 months", "Less than a week").
    /// </summary>
    public string? ProjectDuration { get; set; }

    /// <summary>
    /// Client's rating on Upwork (0-5 scale).
    /// </summary>
    public decimal? ClientRating { get; set; }

    /// <summary>
    /// Total amount the client has spent on Upwork.
    /// </summary>
    public decimal? ClientTotalSpent { get; set; }

    /// <summary>
    /// Client's hire rate percentage.
    /// </summary>
    public decimal? ClientHireRate { get; set; }

    /// <summary>
    /// Number of proposals already submitted to this job.
    /// </summary>
    public int? ProposalsCount { get; set; }

    /// <summary>
    /// Number of Connects required to apply (Upwork credit system).
    /// </summary>
    public int? ConnectsRequired { get; set; }

    /// <summary>
    /// Required skills/tags as JSON array string.
    /// </summary>
    public string? RequiredSkillsJson { get; set; }

    /// <summary>
    /// Gets or sets the required skills as a list (not stored directly in DB).
    /// </summary>
    [NotMapped]
    public List<string> RequiredSkills
    {
        get => string.IsNullOrEmpty(RequiredSkillsJson)
            ? []
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(RequiredSkillsJson) ?? [];
        set => RequiredSkillsJson = value.Count > 0
            ? System.Text.Json.JsonSerializer.Serialize(value)
            : null;
    }
}
