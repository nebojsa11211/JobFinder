using JobFinder.Models;

namespace JobFinder.Services;

public interface ILinkedInService
{
    bool IsLoggedIn { get; }
    bool IsBrowserOpen { get; }
    Task InitializeAsync();
    Task OpenLoginWindowAsync();
    Task<bool> CheckLoginStatusAsync();
    Task<List<Job>> SearchJobsAsync(SearchFilter filter, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
    Task<JobDetails?> GetJobDetailsAsync(string jobUrl);
    Task<bool> StartEasyApplyAsync(string jobUrl);
    Task CloseAsync();
}

public class JobDetails
{
    public string? Description { get; set; }
    public string? RecruiterEmail { get; set; }
    public string? ExternalApplyUrl { get; set; }
    public bool HasEasyApply { get; set; }
}
