using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobFinder.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<JobFinderDbContext>
{
    public JobFinderDbContext CreateDbContext(string[] args)
    {
        return new JobFinderDbContext();
    }
}
