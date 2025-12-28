using System.ComponentModel.DataAnnotations.Schema;

namespace JobFinder.Models;

public class Job
{
    public int Id { get; set; }
    public required string LinkedInJobId { get; set; }
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
}
