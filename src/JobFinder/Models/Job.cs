namespace JobFinder.Models;

public class Job
{
    public int Id { get; set; }
    public required string LinkedInJobId { get; set; }
    public required string Title { get; set; }
    public required string Company { get; set; }
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
}
