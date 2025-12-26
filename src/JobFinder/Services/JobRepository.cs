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

    public async Task<Job?> GetJobByLinkedInIdAsync(string linkedInJobId)
    {
        return await _context.Jobs
            .Include(j => j.Company)
            .FirstOrDefaultAsync(j => j.LinkedInJobId == linkedInJobId);
    }

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

    public async Task<bool> JobExistsAsync(string linkedInJobId)
    {
        return await _context.Jobs.AnyAsync(j => j.LinkedInJobId == linkedInJobId);
    }

    public async Task<int> AddJobsAsync(IEnumerable<Job> jobs)
    {
        int addedCount = 0;
        foreach (var job in jobs)
        {
            if (!await JobExistsAsync(job.LinkedInJobId))
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
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> RestoreAllJobsAsync()
    {
        var discardedJobs = await _context.Jobs.Where(j => j.IsDiscarded).ToListAsync();
        foreach (var job in discardedJobs)
        {
            job.IsDiscarded = false;
        }
        await _context.SaveChangesAsync();
        return discardedJobs.Count;
    }

    public async Task<List<Job>> GetActiveJobsAsync()
    {
        return await _context.Jobs
            .Include(j => j.Company)
            .Where(j => !j.IsDiscarded)
            .OrderByDescending(j => j.DatePosted)
            .ToListAsync();
    }
}
