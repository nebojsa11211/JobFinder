using JobFinder.Models;

namespace JobFinder.Services;

public interface IJobRepository
{
    // ============================================================
    // Query methods
    // ============================================================

    Task<List<Job>> GetAllJobsAsync(bool includeDiscarded = false);
    Task<List<Job>> GetJobsByStatusAsync(ApplicationStatus status, bool includeDiscarded = false);
    Task<List<Job>> GetJobsByPlatformAsync(JobPlatform platform, bool includeDiscarded = false);
    Task<List<Job>> GetActiveJobsAsync();

    /// <summary>
    /// Gets a job by its platform-specific external ID.
    /// </summary>
    Task<Job?> GetJobByExternalIdAsync(JobPlatform platform, string externalJobId);

    /// <summary>
    /// Gets a job by LinkedIn ID (convenience method, calls GetJobByExternalIdAsync).
    /// </summary>
    [Obsolete("Use GetJobByExternalIdAsync instead")]
    Task<Job?> GetJobByLinkedInIdAsync(string linkedInJobId);

    /// <summary>
    /// Checks if a job exists by its platform-specific external ID.
    /// </summary>
    Task<bool> JobExistsAsync(JobPlatform platform, string externalJobId);

    /// <summary>
    /// Checks if a LinkedIn job exists (convenience method, calls JobExistsAsync).
    /// </summary>
    [Obsolete("Use JobExistsAsync(JobPlatform, string) instead")]
    Task<bool> JobExistsAsync(string linkedInJobId);

    // ============================================================
    // Write methods
    // ============================================================

    Task<Job> AddJobAsync(Job job);
    Task<Job> UpdateJobAsync(Job job);

    /// <summary>
    /// Adds multiple jobs, skipping duplicates based on Platform + ExternalJobId.
    /// </summary>
    Task<int> AddJobsAsync(IEnumerable<Job> jobs);

    Task UpdateJobStatusAsync(int jobId, ApplicationStatus status);
    Task DiscardJobAsync(int jobId);
    Task RestoreJobAsync(int jobId);
    Task<int> RestoreAllJobsAsync();

    // ============================================================
    // Initialization
    // ============================================================

    Task InitializeDatabaseAsync();
}
