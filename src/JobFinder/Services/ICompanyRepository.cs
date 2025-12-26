using JobFinder.Models;

namespace JobFinder.Services;

public interface ICompanyRepository
{
    Task<List<Company>> GetAllCompaniesAsync();
    Task<Company?> GetCompanyByIdAsync(int id);
    Task<Company?> GetCompanyByNameAsync(string name);
    Task<Company?> GetCompanyByLinkedInIdAsync(string linkedInId);
    Task<Company> AddCompanyAsync(Company company);
    Task<Company> UpdateCompanyAsync(Company company);
    Task<Company> GetOrCreateCompanyAsync(string name, string? linkedInId = null);
    Task<List<Company>> GetBlacklistedCompaniesAsync();
    Task<List<Company>> GetFavoriteCompaniesAsync();
    Task SetBlacklistedAsync(int companyId, bool isBlacklisted);
    Task SetFavoriteAsync(int companyId, bool isFavorite);
    Task<List<Company>> SearchCompaniesAsync(string searchTerm);
    Task DeleteCompanyAsync(int companyId);
}
