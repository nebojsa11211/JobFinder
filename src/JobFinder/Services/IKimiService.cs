using JobFinder.Models;

namespace JobFinder.Services;

public interface IKimiService
{
    Task<JobSummaryResult?> GetSummaryAsync(string jobDescription, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> ValidateApiKeyAsync();
    bool IsConfigured { get; }
}
