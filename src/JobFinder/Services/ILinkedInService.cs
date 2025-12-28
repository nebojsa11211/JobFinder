using JobFinder.Models;

namespace JobFinder.Services;

public interface ILinkedInService
{
    bool IsLoggedIn { get; }
    bool IsBrowserOpen { get; }

    /// <summary>
    /// Initializes Playwright browser engine.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Opens browser window for LinkedIn login.
    /// </summary>
    Task OpenLoginWindowAsync();

    /// <summary>
    /// Checks if user is logged in and saves session state.
    /// </summary>
    Task<bool> CheckLoginStatusAsync();

    /// <summary>
    /// Searches LinkedIn for jobs matching the filter.
    /// </summary>
    Task<List<Job>> SearchJobsAsync(SearchFilter filter, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets full job details from a job page.
    /// </summary>
    Task<JobDetails?> GetJobDetailsAsync(string jobUrl);

    /// <summary>
    /// Opens Easy Apply modal for manual completion.
    /// </summary>
    Task<bool> StartEasyApplyAsync(string jobUrl);

    /// <summary>
    /// Prepares an Easy Apply application without submitting.
    /// Opens the modal, navigates through pages, and detects all form fields/questions.
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
    /// Fills in the Easy Apply form with provided answers and submits.
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
    /// Cancels an in-progress application by closing the modal.
    /// </summary>
    Task CancelApplicationAsync();

    /// <summary>
    /// Closes the browser and saves session state.
    /// </summary>
    Task CloseAsync();
}

public class JobDetails
{
    public string? Description { get; set; }
    public string? RecruiterEmail { get; set; }
    public string? ExternalApplyUrl { get; set; }
    public bool HasEasyApply { get; set; }
}
