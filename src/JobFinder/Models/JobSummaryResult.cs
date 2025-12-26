namespace JobFinder.Models;

/// <summary>
/// Result from AI job summary analysis including rating and discard recommendation.
/// </summary>
public class JobSummaryResult
{
    /// <summary>
    /// The summary text (translated to Croatian).
    /// </summary>
    public string Summary { get; set; } = "";

    /// <summary>
    /// Short summary (few words) displayed under the job title.
    /// </summary>
    public string ShortSummary { get; set; } = "";

    /// <summary>
    /// Job rating from 1-10 based on match criteria.
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Whether the AI recommends discarding this job.
    /// </summary>
    public bool ShouldDiscard { get; set; }

    /// <summary>
    /// Reason for discard recommendation (if applicable).
    /// </summary>
    public string? DiscardReason { get; set; }

    /// <summary>
    /// Indicates the AI response could not be parsed. Jobs with parse failures
    /// should not be auto-discarded based on rating.
    /// </summary>
    public bool ParseFailed { get; set; }

    /// <summary>
    /// Raw AI response for debugging parse failures.
    /// </summary>
    public string? RawResponse { get; set; }
}
