using JobFinder.Models;

namespace JobFinder.Services;

/// <summary>
/// Factory implementation for obtaining platform-specific job services.
/// Uses dependency injection to resolve registered platform services.
/// </summary>
public class JobPlatformServiceFactory : IJobPlatformServiceFactory
{
    private readonly Dictionary<JobPlatform, IJobPlatformService> _services;

    public JobPlatformServiceFactory(IEnumerable<IJobPlatformService> platformServices)
    {
        _services = platformServices.ToDictionary(s => s.Platform);
    }

    public IJobPlatformService GetService(JobPlatform platform)
    {
        if (_services.TryGetValue(platform, out var service))
        {
            return service;
        }

        throw new NotSupportedException($"Platform '{platform}' is not supported. Available platforms: {string.Join(", ", _services.Keys)}");
    }

    public IEnumerable<IJobPlatformService> GetAllServices()
    {
        return _services.Values;
    }

    public bool IsSupported(JobPlatform platform)
    {
        return _services.ContainsKey(platform);
    }
}
