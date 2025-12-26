using Microsoft.EntityFrameworkCore;
using JobFinder.Data;
using JobFinder.Models;

namespace JobFinder.Services;

public class CompanyRepository : ICompanyRepository
{
    private readonly JobFinderDbContext _context;

    public CompanyRepository(JobFinderDbContext context)
    {
        _context = context;
    }

    public async Task<List<Company>> GetAllCompaniesAsync()
    {
        return await _context.Companies
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Company?> GetCompanyByIdAsync(int id)
    {
        return await _context.Companies
            .Include(c => c.Jobs)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Company?> GetCompanyByNameAsync(string name)
    {
        return await _context.Companies
            .FirstOrDefaultAsync(c => c.Name == name);
    }

    public async Task<Company?> GetCompanyByLinkedInIdAsync(string linkedInId)
    {
        return await _context.Companies
            .FirstOrDefaultAsync(c => c.LinkedInId == linkedInId);
    }

    public async Task<Company> AddCompanyAsync(Company company)
    {
        company.DateAdded = DateTime.Now;
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();
        return company;
    }

    public async Task<Company> UpdateCompanyAsync(Company company)
    {
        company.DateUpdated = DateTime.Now;
        _context.Companies.Update(company);
        await _context.SaveChangesAsync();
        return company;
    }

    public async Task<Company> GetOrCreateCompanyAsync(string name, string? linkedInId = null)
    {
        // First try to find by LinkedIn ID if provided
        Company? company = null;
        if (!string.IsNullOrEmpty(linkedInId))
        {
            company = await GetCompanyByLinkedInIdAsync(linkedInId);
        }

        // Then try by name
        company ??= await GetCompanyByNameAsync(name);

        // Create if not found
        if (company == null)
        {
            company = new Company
            {
                Name = name,
                LinkedInId = linkedInId,
                DateAdded = DateTime.Now
            };
            _context.Companies.Add(company);
            await _context.SaveChangesAsync();
        }

        return company;
    }

    public async Task<List<Company>> GetBlacklistedCompaniesAsync()
    {
        return await _context.Companies
            .Where(c => c.IsBlacklisted)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<List<Company>> GetFavoriteCompaniesAsync()
    {
        return await _context.Companies
            .Where(c => c.IsFavorite)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task SetBlacklistedAsync(int companyId, bool isBlacklisted)
    {
        var company = await _context.Companies.FindAsync(companyId);
        if (company != null)
        {
            company.IsBlacklisted = isBlacklisted;
            company.DateUpdated = DateTime.Now;
            await _context.SaveChangesAsync();
        }
    }

    public async Task SetFavoriteAsync(int companyId, bool isFavorite)
    {
        var company = await _context.Companies.FindAsync(companyId);
        if (company != null)
        {
            company.IsFavorite = isFavorite;
            company.DateUpdated = DateTime.Now;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<Company>> SearchCompaniesAsync(string searchTerm)
    {
        return await _context.Companies
            .Where(c => c.Name.Contains(searchTerm) ||
                       (c.Industry != null && c.Industry.Contains(searchTerm)))
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task DeleteCompanyAsync(int companyId)
    {
        var company = await _context.Companies.FindAsync(companyId);
        if (company != null)
        {
            _context.Companies.Remove(company);
            await _context.SaveChangesAsync();
        }
    }
}
