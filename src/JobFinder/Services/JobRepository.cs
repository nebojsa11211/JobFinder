using Microsoft.EntityFrameworkCore;
using JobFinder.Data;
using JobFinder.Models;

namespace JobFinder.Services;

public class JobRepository : IJobRepository
{
    private readonly JobFinderDbContext _context;
    private readonly ICompanyRepository _companyRepository;

    public JobRepository(JobFinderDbContext context, ICompanyRepository companyRepository)
    {
        _context = context;
        _companyRepository = companyRepository;
    }

    public async Task InitializeDatabaseAsync()
    {
        await _context.MigrateSchemaAsync();
    }

    // ============================================================
    // Query methods
    // ============================================================

    public async Task<List<Job>> GetAllJobsAsync(bool includeDiscarded = false)
    {
        var query = _context.Jobs.Include(j => j.Company).AsQueryable();
        if (!includeDiscarded)
        {
            query = query.Where(j => !j.IsDiscarded);
        }
        return await query
            .OrderByDescending(j => j.DatePosted)
            .ToListAsync();
    }

    public async Task<List<Job>> GetJobsByStatusAsync(ApplicationStatus status, bool includeDiscarded = false)
    {
        var query = _context.Jobs.Include(j => j.Company).Where(j => j.Status == status);
        if (!includeDiscarded)
        {
            query = query.Where(j => !j.IsDiscarded);
        }
        return await query
            .OrderByDescending(j => j.DatePosted)
            .ToListAsync();
    }

    public async Task<List<Job>> GetJobsByPlatformAsync(JobPlatform platform, bool includeDiscarded = false)
    {
        var query = _context.Jobs.Include(j => j.Company).Where(j => j.Platform == platform);
        if (!includeDiscarded)
        {
            query = query.Where(j => !j.IsDiscarded);
        }
        return await query
            .OrderByDescending(j => j.DatePosted)
            .ToListAsync();
    }

    public async Task<List<Job>> GetActiveJobsAsync()
    {
        return await _context.Jobs
            .Include(j => j.Company)
            .Where(j => !j.IsDiscarded)
            .OrderByDescending(j => j.DatePosted)
            .ToListAsync();
    }

    public async Task<Job?> GetJobByExternalIdAsync(JobPlatform platform, string externalJobId)
    {
        return await _context.Jobs
            .Include(j => j.Company)
            .FirstOrDefaultAsync(j => j.Platform == platform && j.ExternalJobId == externalJobId);
    }

    [Obsolete("Use GetJobByExternalIdAsync instead")]
    public async Task<Job?> GetJobByLinkedInIdAsync(string linkedInJobId)
    {
        return await GetJobByExternalIdAsync(JobPlatform.LinkedIn, linkedInJobId);
    }

    public async Task<bool> JobExistsAsync(JobPlatform platform, string externalJobId)
    {
        return await _context.Jobs.AnyAsync(j => j.Platform == platform && j.ExternalJobId == externalJobId);
    }

    [Obsolete("Use JobExistsAsync(JobPlatform, string) instead")]
    public async Task<bool> JobExistsAsync(string linkedInJobId)
    {
        return await JobExistsAsync(JobPlatform.LinkedIn, linkedInJobId);
    }

    // ============================================================
    // Write methods
    // ============================================================

    public async Task<Job> AddJobAsync(Job job)
    {
        // Link job to company
        await LinkJobToCompanyAsync(job);

        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();
        return job;
    }

    private async Task LinkJobToCompanyAsync(Job job)
    {
        if (job.CompanyId == 0 && !string.IsNullOrEmpty(job.ScrapedCompanyName))
        {
            var company = await _companyRepository.GetOrCreateCompanyAsync(job.ScrapedCompanyName);
            job.CompanyId = company.Id;
            job.Company = company;
        }
    }

    public async Task<Job> UpdateJobAsync(Job job)
    {
        _context.Jobs.Update(job);
        await _context.SaveChangesAsync();
        return job;
    }

    public async Task<int> AddJobsAsync(IEnumerable<Job> jobs)
    {
        int addedCount = 0;
        foreach (var job in jobs)
        {
            if (!await JobExistsAsync(job.Platform, job.ExternalJobId))
            {
                // Link job to company
                await LinkJobToCompanyAsync(job);

                _context.Jobs.Add(job);
                addedCount++;
            }
        }
        await _context.SaveChangesAsync();
        return addedCount;
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

    public async Task DiscardJobAsync(int jobId)
    {
        var job = await _context.Jobs.FindAsync(jobId);
        if (job != null)
        {
            job.IsDiscarded = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task RestoreJobAsync(int jobId)
    {
        var job = await _context.Jobs.FindAsync(jobId);
        if (job != null)
        {
            job.IsDiscarded = false;
            job.SummaryCroatian = null;
            job.ShortSummary = null;
            job.Rating = null;
            job.DiscardReason = null;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> RestoreAllJobsAsync()
    {
        // Use ExecuteUpdateAsync to bypass change tracking issues
        // This executes a direct SQL UPDATE on ALL jobs:
        // - Restores discarded jobs
        // - Clears summaries on ALL jobs so they can be re-analyzed
        var count = await _context.Jobs
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.IsDiscarded, false)
                .SetProperty(j => j.SummaryCroatian, (string?)null)
                .SetProperty(j => j.ShortSummary, (string?)null)
                .SetProperty(j => j.Rating, (int?)null)
                .SetProperty(j => j.DiscardReason, (string?)null));

        // Clear the change tracker to ensure LoadJobsAsync gets fresh data
        _context.ChangeTracker.Clear();

        return count;
    }
}
