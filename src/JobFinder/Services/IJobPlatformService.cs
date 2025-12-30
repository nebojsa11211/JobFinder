using JobFinder.Models;

namespace JobFinder.Services;

/// <summary>
/// Generic interface for job platform services (LinkedIn, Upwork, etc.).
/// Provides common operations for browser automation, job search, and application submission.
/// </summary>
public interface IJobPlatformService
{
    /// <summary>
    /// The platform this service handles.
    /// </summary>
    JobPlatform Platform { get; }

    /// <summary>
    /// Whether the user is currently logged in to the platform.
    /// </summary>
    bool IsLoggedIn { get; }

    /// <summary>
    /// Whether the browser window is currently open.
    /// </summary>
    bool IsBrowserOpen { get; }

    /// <summary>
    /// Initializes the Playwright browser engine.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Opens browser window for platform login.
    /// </summary>
    Task OpenLoginWindowAsync();

    /// <summary>
    /// Checks if user is logged in and saves session state.
    /// </summary>
    /// <returns>True if logged in, false otherwise.</returns>
    Task<bool> CheckLoginStatusAsync();

    /// <summary>
    /// Searches the platform for jobs matching the filter.
    /// </summary>
    /// <param name="filter">Search parameters.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of jobs found.</returns>
    Task<List<Job>> SearchJobsAsync(
        SearchFilter filter,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets full job details from a job page.
    /// </summary>
    /// <param name="jobUrl">URL of the job listing.</param>
    /// <returns>Job details or null if not found.</returns>
    Task<JobDetails?> GetJobDetailsAsync(string jobUrl);

    /// <summary>
    /// Prepares an application without submitting.
    /// Opens the application form, navigates through pages, and detects all form fields/questions.
    /// </summary>
    /// <param name="job">The job to apply to.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Application session with detected questions, or null if failed.</returns>
    Task<ApplicationSession?> PrepareApplicationAsync(
        Job job,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fills in the application form with provided answers and submits.
    /// Only call after user approval in the review dialog.
    /// </summary>
    /// <param name="session">The approved application session with answers.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if submission succeeded.</returns>
    Task<bool> SubmitApplicationAsync(
        ApplicationSession session,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an in-progress application by closing the form/modal.
    /// </summary>
    Task CancelApplicationAsync();

    /// <summary>
    /// Closes the browser and saves session state.
    /// </summary>
    Task CloseAsync();
}

/// <summary>
/// Extended job details returned from a job page.
/// Platform-agnostic structure with optional platform-specific fields.
/// </summary>
public class JobDetails
{
    /// <summary>
    /// Full job description text.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Recruiter or contact email if available.
    /// </summary>
    public string? RecruiterEmail { get; set; }

    /// <summary>
    /// External URL for applying (non-platform application).
    /// </summary>
    public string? ExternalApplyUrl { get; set; }

    /// <summary>
    /// Whether the job supports quick/easy apply on the platform.
    /// LinkedIn: Easy Apply, Upwork: Direct proposal submission.
    /// </summary>
    public bool HasEasyApply { get; set; }

    // Upwork-specific fields

    /// <summary>
    /// Minimum hourly rate (Upwork hourly jobs).
    /// </summary>
    public decimal? HourlyRateMin { get; set; }

    /// <summary>
    /// Maximum hourly rate (Upwork hourly jobs).
    /// </summary>
    public decimal? HourlyRateMax { get; set; }

    /// <summary>
    /// Fixed price budget (Upwork fixed-price jobs).
    /// </summary>
    public decimal? FixedPrice { get; set; }

    /// <summary>
    /// Client's rating on the platform.
    /// </summary>
    public decimal? ClientRating { get; set; }

    /// <summary>
    /// Total amount client has spent on the platform.
    /// </summary>
    public decimal? ClientTotalSpent { get; set; }

    /// <summary>
    /// Client's hire rate percentage.
    /// </summary>
    public decimal? ClientHireRate { get; set; }

    /// <summary>
    /// Number of proposals/applications already submitted.
    /// </summary>
    public int? ProposalsCount { get; set; }

    /// <summary>
    /// Required skills/tags for the job.
    /// </summary>
    public List<string>? RequiredSkills { get; set; }

    /// <summary>
    /// Estimated project duration.
    /// </summary>
    public string? ProjectDuration { get; set; }

    /// <summary>
    /// Experience level required.
    /// </summary>
    public string? ExperienceLevel { get; set; }
}
