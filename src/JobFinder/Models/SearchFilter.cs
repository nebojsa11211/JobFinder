namespace JobFinder.Models;

public class SearchFilter
{
    public string JobTitle { get; set; } = ".NET Developer";
    public List<string> ExperienceLevels { get; set; } = ["Mid-Senior level", "Senior"];
    public List<string> Locations { get; set; } = ["Croatia", "Slovenia", "Europe"];
    public bool RemoteOnly { get; set; } = true;
    public int MaxResults { get; set; } = 100;
}
