using JobFinder.Models;

namespace JobFinder.Services;

/// <summary>
/// LinkedIn-specific job platform service.
/// Extends the generic IJobPlatformService with LinkedIn-specific operations.
/// </summary>
public interface ILinkedInService : IJobPlatformService
{
    /// <summary>
    /// Opens Easy Apply modal for manual completion.
    /// LinkedIn-specific: allows user to complete the application manually.
    /// </summary>
    /// <param name="jobUrl">URL of the job listing.</param>
    /// <returns>True if modal was opened successfully.</returns>
    Task<bool> StartEasyApplyAsync(string jobUrl);
}
