using JobFinder.Models;

namespace JobFinder.Services;

/// <summary>
/// Factory for obtaining platform-specific job services.
/// </summary>
public interface IJobPlatformServiceFactory
{
    /// <summary>
    /// Gets the job platform service for the specified platform.
    /// </summary>
    /// <param name="platform">The platform to get the service for.</param>
    /// <returns>The platform-specific service.</returns>
    /// <exception cref="NotSupportedException">Thrown if the platform is not supported.</exception>
    IJobPlatformService GetService(JobPlatform platform);

    /// <summary>
    /// Gets all available platform services.
    /// </summary>
    /// <returns>Enumerable of all registered platform services.</returns>
    IEnumerable<IJobPlatformService> GetAllServices();

    /// <summary>
    /// Checks if a platform is supported.
    /// </summary>
    /// <param name="platform">The platform to check.</param>
    /// <returns>True if supported, false otherwise.</returns>
    bool IsSupported(JobPlatform platform);
}
