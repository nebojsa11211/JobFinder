namespace JobFinder.Models;

public class Company
{
    public int Id { get; set; }

    /// <summary>
    /// Company name as displayed on LinkedIn.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// LinkedIn company identifier (from URL or data attributes).
    /// </summary>
    public string? LinkedInId { get; set; }

    /// <summary>
    /// Full LinkedIn company profile URL.
    /// </summary>
    public string? LinkedInUrl { get; set; }

    /// <summary>
    /// Company website URL.
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Industry sector (e.g., "Information Technology", "Healthcare").
    /// </summary>
    public string? Industry { get; set; }

    /// <summary>
    /// Company size range (e.g., "51-200 employees", "1001-5000 employees").
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    /// Headquarters location.
    /// </summary>
    public string? Headquarters { get; set; }

    /// <summary>
    /// Company description/about text.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// URL to company logo image.
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// User notes about the company.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// If true, jobs from this company will be auto-discarded.
    /// </summary>
    public bool IsBlacklisted { get; set; }

    /// <summary>
    /// If true, this company is marked as a favorite.
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Date when this company was first scraped.
    /// </summary>
    public DateTime DateAdded { get; set; } = DateTime.Now;

    /// <summary>
    /// Date when company info was last updated.
    /// </summary>
    public DateTime? DateUpdated { get; set; }

    /// <summary>
    /// Navigation property to jobs from this company.
    /// </summary>
    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}
