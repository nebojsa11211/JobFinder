using JobFinder.Models;

namespace JobFinder.Services;

public interface IJobRepository
{
    Task<List<Job>> GetAllJobsAsync(bool includeDiscarded = false);
    Task<List<Job>> GetJobsByStatusAsync(ApplicationStatus status, bool includeDiscarded = false);
    Task<Job?> GetJobByLinkedInIdAsync(string linkedInJobId);
    Task<Job> AddJobAsync(Job job);
    Task<Job> UpdateJobAsync(Job job);
    Task<bool> JobExistsAsync(string linkedInJobId);
    Task<int> AddJobsAsync(IEnumerable<Job> jobs);
    Task UpdateJobStatusAsync(int jobId, ApplicationStatus status);
    Task DiscardJobAsync(int jobId);
    Task RestoreJobAsync(int jobId);
    Task<int> RestoreAllJobsAsync();
    Task<List<Job>> GetActiveJobsAsync();
    Task InitializeDatabaseAsync();
}
