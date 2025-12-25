using JobFinder.Models;

namespace JobFinder.Services;

public interface IJobRepository
{
    Task<List<Job>> GetAllJobsAsync();
    Task<List<Job>> GetJobsByStatusAsync(ApplicationStatus status);
    Task<Job?> GetJobByLinkedInIdAsync(string linkedInJobId);
    Task<Job> AddJobAsync(Job job);
    Task<Job> UpdateJobAsync(Job job);
    Task<bool> JobExistsAsync(string linkedInJobId);
    Task AddJobsAsync(IEnumerable<Job> jobs);
    Task UpdateJobStatusAsync(int jobId, ApplicationStatus status);
    Task InitializeDatabaseAsync();
}
