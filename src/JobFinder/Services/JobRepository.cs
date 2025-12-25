using Microsoft.EntityFrameworkCore;
using JobFinder.Data;
using JobFinder.Models;

namespace JobFinder.Services;

public class JobRepository : IJobRepository
{
    private readonly JobFinderDbContext _context;

    public JobRepository(JobFinderDbContext context)
    {
        _context = context;
    }

    public async Task InitializeDatabaseAsync()
    {
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task<List<Job>> GetAllJobsAsync()
    {
        return await _context.Jobs
            .OrderByDescending(j => j.DatePosted)
            .ToListAsync();
    }

    public async Task<List<Job>> GetJobsByStatusAsync(ApplicationStatus status)
    {
        return await _context.Jobs
            .Where(j => j.Status == status)
            .OrderByDescending(j => j.DatePosted)
            .ToListAsync();
    }

    public async Task<Job?> GetJobByLinkedInIdAsync(string linkedInJobId)
    {
        return await _context.Jobs
            .FirstOrDefaultAsync(j => j.LinkedInJobId == linkedInJobId);
    }

    public async Task<Job> AddJobAsync(Job job)
    {
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();
        return job;
    }

    public async Task<Job> UpdateJobAsync(Job job)
    {
        _context.Jobs.Update(job);
        await _context.SaveChangesAsync();
        return job;
    }

    public async Task<bool> JobExistsAsync(string linkedInJobId)
    {
        return await _context.Jobs.AnyAsync(j => j.LinkedInJobId == linkedInJobId);
    }

    public async Task AddJobsAsync(IEnumerable<Job> jobs)
    {
        foreach (var job in jobs)
        {
            if (!await JobExistsAsync(job.LinkedInJobId))
            {
                _context.Jobs.Add(job);
            }
        }
        await _context.SaveChangesAsync();
    }

    public async Task UpdateJobStatusAsync(int jobId, ApplicationStatus status)
    {
        var job = await _context.Jobs.FindAsync(jobId);
        if (job != null)
        {
            job.Status = status;
            if (status == ApplicationStatus.Applied)
            {
                job.DateApplied = DateTime.Now;
            }
            await _context.SaveChangesAsync();
        }
    }
}
