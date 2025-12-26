using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using JobFinder.Models;

namespace JobFinder.Services;

public partial class LinkedInService : ILinkedInService, IAsyncDisposable
{
    private readonly ISettingsService _settingsService;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private bool _isLoggedIn;
    private readonly string _userDataDir;

    public bool IsLoggedIn => _isLoggedIn;
    public bool IsBrowserOpen => _browser != null;

    public LinkedInService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _userDataDir = Path.Combine(appData, "JobFinder", "browser-data");
        Directory.CreateDirectory(_userDataDir);
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
    }

    public async Task OpenLoginWindowAsync()
    {
        if (_playwright == null)
            await InitializeAsync();

        // Launch browser in headed mode for manual login
        var browserArgs = _settingsService.Settings.StartBrowserMinimized
            ? new[] { "--start-minimized" }
            : new[] { "--start-maximized" };

        _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            Args = browserArgs
        });

        // Only load storage state if file exists (for returning users)
        var storageStatePath = GetStorageStatePath();
        var contextOptions = new BrowserNewContextOptions
        {
            ViewportSize = ViewportSize.NoViewport
        };

        if (File.Exists(storageStatePath))
        {
            contextOptions.StorageStatePath = storageStatePath;
        }

        _context = await _browser.NewContextAsync(contextOptions);
        _page = await _context.NewPageAsync();

        // Navigate to feed if we have stored credentials, otherwise to login
        await _page.GotoAsync("https://www.linkedin.com/feed");
    }

    public async Task<bool> CheckLoginStatusAsync()
    {
        if (_page == null) return false;

        try
        {
            var currentUrl = _page.Url;

            // If we're on the login page, we're not logged in
            if (currentUrl.Contains("/login") || currentUrl.Contains("/checkpoint") || currentUrl.Contains("/authwall"))
            {
                _isLoggedIn = false;
                return false;
            }

            // Check for authenticated-only elements that don't exist on public pages
            // The global nav with profile menu is only visible when logged in
            var profileNav = _page.Locator("div.global-nav__me, .nav-item__profile-member-photo, img.global-nav__me-photo, button[data-control-name='nav.settings']");
            var hasProfileNav = await profileNav.CountAsync() > 0;

            // Also check for the "Sign in" button - if it exists, we're NOT logged in
            var signInButton = _page.Locator("a[href*='login'], a:has-text('Sign in')").First;
            var hasSignInButton = await signInButton.CountAsync() > 0;

            // Check if we're on the feed page (only accessible when logged in)
            var isOnFeed = currentUrl.Contains("/feed") && !currentUrl.Contains("session_redirect");

            _isLoggedIn = (hasProfileNav || isOnFeed) && !hasSignInButton;

            if (_isLoggedIn && _context != null)
            {
                // Save storage state for future sessions
                await _context.StorageStateAsync(new BrowserContextStorageStateOptions
                {
                    Path = GetStorageStatePath()
                });
            }

            return _isLoggedIn;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<Job>> SearchJobsAsync(SearchFilter filter, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var jobs = new List<Job>();

        if (_page == null)
        {
            progress?.Report("Browser not open. Please open login window first.");
            return jobs;
        }

        if (!_isLoggedIn)
        {
            progress?.Report("Not logged in - searching public job listings (limited results)...");
        }

        try
        {
            // Build search URL
            var searchUrl = BuildSearchUrl(filter);
            progress?.Report($"Navigating to LinkedIn Jobs...");

            await _page.GotoAsync(searchUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await Task.Delay(3000, cancellationToken);

            // Verify we're still logged in (session might have expired)
            var signInPrompt = await _page.Locator("button:has-text('Sign in'), a:has-text('Sign in')").CountAsync();
            if (signInPrompt > 0)
            {
                // Check if it's just a small sign-in link in nav (acceptable) vs a login wall
                var loginWall = await _page.Locator(".authwall-join-form, .login-form, [data-test-modal-id='join-now-modal']").CountAsync();
                if (loginWall > 0)
                {
                    progress?.Report("Session expired. Please log in again.");
                    _isLoggedIn = false;
                    return jobs;
                }
            }

            // Dismiss any cookie consent or promotional modals that might be blocking
            try
            {
                // Use JavaScript to remove modals (more reliable than clicking)
                await _page.EvaluateAsync("document.querySelectorAll('.top-level-modal-container, .contextual-sign-in-modal, [role=\"dialog\"]').forEach(el => el.remove())");
                await Task.Delay(500, cancellationToken);
            }
            catch { /* Ignore modal dismissal errors */ }

            int pageNum = 0;
            int totalScraped = 0;
            var processedJobIds = new HashSet<string>();

            while (totalScraped < filter.MaxResults && !cancellationToken.IsCancellationRequested)
            {
                progress?.Report($"Scraping page {pageNum + 1}...");

                // Scroll to load all jobs on the page
                await ScrollJobListAsync();
                await Task.Delay(2000, cancellationToken);

                // Use Playwright Locator API to find all job links
                var jobLinksLocator = _page.Locator("a[href*='/jobs/view/']");
                var linkCount = await jobLinksLocator.CountAsync();
                progress?.Report($"Found {linkCount} job links on page");

                if (linkCount == 0)
                {
                    // Debug: check what's on the page
                    var pageTitle = await _page.TitleAsync();
                    var pageUrl = _page.Url;
                    progress?.Report($"Page: {pageTitle}");
                    progress?.Report($"URL: {pageUrl}");

                    // Check if redirected to login
                    if (pageUrl.Contains("/login") || pageUrl.Contains("/authwall") || pageUrl.Contains("/checkpoint"))
                    {
                        progress?.Report("Redirected to login page - session may have expired");
                        _isLoggedIn = false;
                        break;
                    }

                    // Try to find job list containers (different possible classes)
                    var jobListCount = await _page.Locator(".jobs-search-results-list, .scaffold-layout__list-container, ul.jobs-search__results-list").CountAsync();
                    progress?.Report($"Job list containers found: {jobListCount}");

                    // Check for "no results" message
                    var noResults = await _page.Locator("text=No matching jobs, text=No results found").CountAsync();
                    if (noResults > 0)
                    {
                        progress?.Report("LinkedIn returned no matching jobs for this search");
                        break;
                    }

                    // Try alternative selectors for job cards
                    var altJobCards = await _page.Locator(".job-card-container, .jobs-search-results__list-item, li.jobs-search-results__list-item").CountAsync();
                    progress?.Report($"Alternative job cards found: {altJobCards}");

                    // Try to find any links at all
                    var allLinksCount = await _page.Locator("a").CountAsync();
                    progress?.Report($"Total links on page: {allLinksCount}");

                    break;
                }

                // Process each job link using Locator API
                for (int i = 0; i < linkCount && totalScraped < filter.MaxResults; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        var link = jobLinksLocator.Nth(i);
                        var href = await link.GetAttributeAsync("href") ?? "";

                        // Extract job ID from URL
                        var jobIdMatch = JobIdRegex().Match(href);
                        if (!jobIdMatch.Success)
                            continue;

                        var jobId = jobIdMatch.Groups[1].Value;

                        // Skip if already processed
                        if (processedJobIds.Contains(jobId))
                            continue;
                        processedJobIds.Add(jobId);

                        // Get title from link text
                        var title = await link.InnerTextAsync();
                        title = title?.Trim() ?? "";

                        if (string.IsNullOrWhiteSpace(title))
                        {
                            // Try aria-label
                            title = await link.GetAttributeAsync("aria-label") ?? "Unknown";
                        }

                        // Find parent card to get company and location
                        // Go up to the list item container
                        var card = link.Locator("xpath=ancestor::li[1]");
                        var cardExists = await card.CountAsync() > 0;

                        string company = "Unknown";
                        string location = "Unknown";
                        bool hasEasyApply = false;
                        string postedDate = "";

                        if (cardExists)
                        {
                            // Get all text from the card and parse it
                            var cardText = await card.InnerTextAsync();
                            var lines = cardText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                               .Select(l => l.Trim())
                                               .Where(l => !string.IsNullOrWhiteSpace(l))
                                               .ToList();

                            // Usually: Title, Company, Location, Time, [Viewed/Easy Apply]
                            // Find company (usually after title, before location)
                            for (int j = 0; j < lines.Count; j++)
                            {
                                var line = lines[j];

                                // Skip the title line
                                if (line.Contains(title.Split('–')[0].Trim().Substring(0, Math.Min(20, title.Split('–')[0].Trim().Length))))
                                    continue;

                                // Look for location patterns (contains "Remote" or location keywords)
                                if (line.Contains("Remote") || line.Contains("(") ||
                                    line.Contains("Europe") || line.Contains("Croatia") ||
                                    line.Contains("EMEA") || line.Contains("Union"))
                                {
                                    if (location == "Unknown")
                                        location = line;
                                }
                                // Time patterns
                                else if (line.Contains("ago") || line.Contains("hour") ||
                                         line.Contains("minute") || line.Contains("day") ||
                                         line.Contains("week") || line.Contains("month"))
                                {
                                    postedDate = line;
                                }
                                // Easy Apply check
                                else if (line.Equals("Easy Apply", StringComparison.OrdinalIgnoreCase))
                                {
                                    hasEasyApply = true;
                                }
                                // Status indicators to skip
                                else if (line == "Viewed" || line == "Applied" || line == "×")
                                {
                                    continue;
                                }
                                // Otherwise it might be company name
                                else if (company == "Unknown" && line.Length > 2 && line.Length < 100)
                                {
                                    company = line;
                                }
                            }

                            // Check for Easy Apply in full text
                            if (!hasEasyApply && cardText.Contains("Easy Apply", StringComparison.OrdinalIgnoreCase))
                            {
                                hasEasyApply = true;
                            }
                        }

                        var job = new Job
                        {
                            LinkedInJobId = jobId,
                            Title = title,
                            ScrapedCompanyName = company,
                            Location = location,
                            JobUrl = href.StartsWith("http") ? href : $"https://www.linkedin.com{href}",
                            HasEasyApply = hasEasyApply,
                            DatePosted = DateTime.Now, // We'll use scraped time since relative dates are hard to parse
                            DateScraped = DateTime.Now,
                            Status = ApplicationStatus.New
                        };

                        jobs.Add(job);
                        totalScraped++;
                        progress?.Report($"Found: {job.Title} at {job.ScrapedCompanyName}");
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"Error on job {i}: {ex.Message}");
                    }
                }

                if (totalScraped >= filter.MaxResults)
                    break;

                // Try to go to next page
                var nextButton = _page.Locator("button[aria-label='View next page'], button[aria-label='Page forward']").First;
                var nextExists = await nextButton.CountAsync() > 0;

                if (!nextExists)
                {
                    // Try numbered pagination
                    var nextPageNum = pageNum + 2;
                    nextButton = _page.Locator($"button[aria-label='Page {nextPageNum}']").First;
                    nextExists = await nextButton.CountAsync() > 0;
                }

                if (!nextExists)
                {
                    progress?.Report("No more pages available.");
                    break;
                }

                var isEnabled = await nextButton.IsEnabledAsync();
                if (!isEnabled)
                {
                    progress?.Report("Next button disabled.");
                    break;
                }

                await nextButton.ClickAsync();
                await Task.Delay(3000, cancellationToken);
                pageNum++;
            }

            progress?.Report($"Completed. Found {jobs.Count} jobs.");
        }
        catch (Exception ex)
        {
            progress?.Report($"Search error: {ex.Message}");
        }

        return jobs;
    }

    private async Task ScrollJobListAsync()
    {
        // Try multiple selectors for the job list container
        var jobsList = await _page!.QuerySelectorAsync("ul.scaffold-layout__list-container, .jobs-search-results-list, .jobs-search__results-list");
        if (jobsList != null)
        {
            // Scroll down the job list to load all items
            for (int i = 0; i < 8; i++)
            {
                await jobsList.EvaluateAsync("el => el.scrollTop = el.scrollTop + 400");
                await Task.Delay(400);
            }
            // Scroll back to top
            await jobsList.EvaluateAsync("el => el.scrollTop = 0");
            await Task.Delay(300);
        }
        else
        {
            // Fallback: scroll the whole page
            for (int i = 0; i < 5; i++)
            {
                await _page.EvaluateAsync("window.scrollBy(0, 500)");
                await Task.Delay(400);
            }
            await _page.EvaluateAsync("window.scrollTo(0, 0)");
        }
    }

    private async Task<Job?> ExtractJobFromCardAsync(IElementHandle card)
    {
        // Try multiple selectors for job title - the title link is often the main link
        var titleElement = await card.QuerySelectorAsync("a.job-card-list__title, a.job-card-container__link, .job-card-list__title, .artdeco-entity-lockup__title a, strong");

        // Try multiple selectors for company name
        var companyElement = await card.QuerySelectorAsync(".job-card-container__company-name, .job-card-container__primary-description, .artdeco-entity-lockup__subtitle span, .job-card-container__underline-wrapper span");

        // Try multiple selectors for location
        var locationElement = await card.QuerySelectorAsync(".job-card-container__metadata-item, .job-card-container__metadata-wrapper li, .artdeco-entity-lockup__caption span");

        // The title element is often also the link
        var linkElement = await card.QuerySelectorAsync("a.job-card-list__title, a.job-card-container__link, a[href*='/jobs/view/']");

        var timeElement = await card.QuerySelectorAsync("time");

        // If no link found, try to get it from the card's data attribute or any anchor
        if (linkElement == null)
        {
            linkElement = await card.QuerySelectorAsync("a[href*='linkedin.com/jobs']");
        }

        if (titleElement == null && linkElement == null)
            return null;

        // Get title text - if titleElement is null, try to get it from link
        string title = "Unknown";
        if (titleElement != null)
        {
            title = await titleElement.InnerTextAsync();
        }
        else if (linkElement != null)
        {
            title = await linkElement.InnerTextAsync();
        }

        var company = companyElement != null ? await companyElement.InnerTextAsync() : "Unknown";
        var location = locationElement != null ? await locationElement.InnerTextAsync() : "Unknown";

        // Get href from link or from data attribute
        var href = "";
        if (linkElement != null)
        {
            href = await linkElement.GetAttributeAsync("href") ?? "";
        }

        // Also try to get job ID from data attribute on the card itself
        var dataJobId = await card.GetAttributeAsync("data-occludable-job-id") ??
                        await card.GetAttributeAsync("data-job-id");

        var dateText = timeElement != null ? await timeElement.GetAttributeAsync("datetime") : null;

        // Extract job ID from URL or use data attribute
        string jobId;
        if (!string.IsNullOrEmpty(dataJobId))
        {
            jobId = dataJobId;
        }
        else
        {
            var jobIdMatch = JobIdRegex().Match(href);
            jobId = jobIdMatch.Success ? jobIdMatch.Groups[1].Value : Guid.NewGuid().ToString();
        }

        // Check for Easy Apply badge - multiple possible selectors
        var easyApplyBadge = await card.QuerySelectorAsync(".job-card-container__apply-method, .job-card-container__footer-job-state, [class*='easy-apply']");
        var hasEasyApply = false;
        if (easyApplyBadge != null)
        {
            var badgeText = await easyApplyBadge.InnerTextAsync();
            hasEasyApply = badgeText.Contains("Easy Apply", StringComparison.OrdinalIgnoreCase);
        }

        // Build job URL if we only have the ID
        if (string.IsNullOrEmpty(href) && !string.IsNullOrEmpty(jobId) && jobId != Guid.Empty.ToString())
        {
            href = $"https://www.linkedin.com/jobs/view/{jobId}/";
        }

        return new Job
        {
            LinkedInJobId = jobId,
            Title = title.Trim(),
            ScrapedCompanyName = company.Trim(),
            Location = location.Trim(),
            JobUrl = href.StartsWith("http") ? href : $"https://www.linkedin.com{href}",
            HasEasyApply = hasEasyApply,
            DatePosted = DateTime.TryParse(dateText, out var date) ? date : DateTime.Now,
            DateScraped = DateTime.Now,
            Status = ApplicationStatus.New
        };
    }

    public async Task<JobDetails?> GetJobDetailsAsync(string jobUrl)
    {
        if (_page == null)
            return null;

        try
        {
            // Use JavaScript navigation to preserve session context
            // Direct GotoAsync loses cookies and causes LinkedIn to block access
            await _page.EvaluateAsync($"window.location.href = '{jobUrl.Replace("'", "\\'")}'");

            // Wait for navigation to complete
            try
            {
                await _page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 10000 });
            }
            catch (TimeoutException)
            {
                // Continue even if timeout - page might still be usable
            }
            await Task.Delay(2000);

            // Dismiss any sign-in modals that might be blocking (common on public job pages)
            try
            {
                // Remove modal via JavaScript (more reliable than clicking)
                await _page.EvaluateAsync("document.querySelectorAll('.top-level-modal-container, .contextual-sign-in-modal').forEach(el => el.remove())");
                await Task.Delay(300);
            }
            catch { }

            var details = new JobDetails();

            // Try to click "Show more" button to expand the full description
            try
            {
                var showMoreButton = _page.Locator("button:has-text('Show more')").First;
                if (await showMoreButton.CountAsync() > 0 && await showMoreButton.IsVisibleAsync())
                {
                    await showMoreButton.ClickAsync();
                    await Task.Delay(500);
                }
            }
            catch { }

            // Get description - try multiple selectors for both logged-in and public pages
            // Public pages: description is in .description__text or .show-more-less-html__markup
            // The content might be in various containers depending on the page version
            string? description = null;

            // Try public page selectors first
            var selectors = new[]
            {
                ".show-more-less-html__markup",  // Public page expanded description
                ".show-more-less-html",           // Public page description container
                ".description__text",             // Another public page format
                ".jobs-description-content__text", // Logged-in page
                ".jobs-description__content",     // Logged-in page variant
                ".jobs-box__html-content",        // Another variant
                "article.jobs-description",       // Article-based layout
                "section.description"             // Section-based layout
            };

            foreach (var selector in selectors)
            {
                var element = await _page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    description = await element.InnerTextAsync();
                    if (!string.IsNullOrWhiteSpace(description) && description.Length > 50)
                    {
                        break;
                    }
                }
            }

            // Fallback: try to get description from the main content area by looking for job details section
            if (string.IsNullOrWhiteSpace(description) || description.Length < 50)
            {
                // On public pages, description is often after the topcard in a specific section
                var mainContent = await _page.QuerySelectorAsync("main section, .top-card-layout + section, .decorated-job-posting__details");
                if (mainContent != null)
                {
                    var text = await mainContent.InnerTextAsync();
                    // Extract just the description part (before "Seniority level" metadata)
                    var seniorityIndex = text.IndexOf("Seniority level", StringComparison.OrdinalIgnoreCase);
                    if (seniorityIndex > 100)
                    {
                        description = text.Substring(0, seniorityIndex).Trim();
                    }
                    else if (text.Length > 100)
                    {
                        description = text;
                    }
                }
            }

            details.Description = description;

            // Check for Easy Apply button
            var easyApplyButton = await _page.QuerySelectorAsync("button:has-text('Easy Apply'), .jobs-apply-button--top-card, .jobs-apply-button");
            if (easyApplyButton != null)
            {
                var buttonText = await easyApplyButton.InnerTextAsync();
                details.HasEasyApply = buttonText.Contains("Easy Apply", StringComparison.OrdinalIgnoreCase);
            }

            // Look for external apply link (Apply button without Easy Apply)
            if (!details.HasEasyApply)
            {
                var applyButton = await _page.QuerySelectorAsync("button:has-text('Apply')");
                if (applyButton != null)
                {
                    // On public pages, clicking Apply typically redirects to external site
                    // We don't click it here to avoid navigation, but note the job may have external apply
                    details.HasEasyApply = false;
                }
            }

            // Try to find recruiter email in description
            if (details.Description != null)
            {
                var emailMatch = EmailRegex().Match(details.Description);
                if (emailMatch.Success)
                {
                    details.RecruiterEmail = emailMatch.Value;
                }
            }

            return details;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> StartEasyApplyAsync(string jobUrl)
    {
        if (_page == null || !_isLoggedIn)
            return false;

        try
        {
            await _page.GotoAsync(jobUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await Task.Delay(2000);

            // Click Easy Apply button - try multiple selectors
            var easyApplyButton = await _page.QuerySelectorAsync(".jobs-apply-button--top-card, .jobs-apply-button, button.jobs-apply-button, [data-job-apply-button]");
            if (easyApplyButton == null)
                return false;

            var buttonText = await easyApplyButton.InnerTextAsync();
            if (!buttonText.Contains("Easy Apply", StringComparison.OrdinalIgnoreCase))
                return false;

            await easyApplyButton.ClickAsync();
            await Task.Delay(1500);

            // The modal is now open - user can complete manually
            // We could automate more steps here but it's safer to let user review
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string BuildSearchUrl(SearchFilter filter)
    {
        // Use public jobs search URL format - MUST have trailing slash before query string
        // Without trailing slash, LinkedIn redirects to authwall
        var baseUrl = "https://www.linkedin.com/jobs/search/?";

        // Use + for spaces instead of %20 (LinkedIn prefers this format)
        var keywords = filter.JobTitle.Replace(" ", "+");
        var parameters = new List<string>
        {
            $"keywords={keywords}"
        };

        // Location is REQUIRED for public job search to work without auth redirect
        // If no location specified, default to "United States"
        var location = filter.Locations.Count > 0 ? filter.Locations[0] : "United States";
        parameters.Add($"location={location.Replace(" ", "+")}");

        // Remote filter (f_WT=2 means remote)
        if (filter.RemoteOnly)
        {
            parameters.Add("f_WT=2");
        }

        // Experience level (f_E=3 is Associate, 4 is Mid-Senior, 5 is Director)
        var expLevels = new List<string>();
        foreach (var level in filter.ExperienceLevels)
        {
            if (level.Contains("Mid") || level.Contains("Senior"))
                expLevels.Add("4");
            if (level.Contains("Director"))
                expLevels.Add("5");
        }
        if (expLevels.Count > 0)
        {
            parameters.Add($"f_E={string.Join(",", expLevels.Distinct())}");
        }

        // Note: sortBy=DD (sort by date) requires authentication and causes redirect to login
        // Removed for now as it causes auth issues even with stored session

        return baseUrl + string.Join("&", parameters);
    }

    private string GetStorageStatePath()
    {
        return Path.Combine(_userDataDir, "storage-state.json");
    }

    public async Task CloseAsync()
    {
        if (_context != null && _isLoggedIn)
        {
            try
            {
                await _context.StorageStateAsync(new BrowserContextStorageStateOptions
                {
                    Path = GetStorageStatePath()
                });
            }
            catch { }
        }

        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        _page = null;
        _context = null;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _playwright?.Dispose();
    }

    [GeneratedRegex(@"/view/(\d+)")]
    private static partial Regex JobIdRegex();

    [GeneratedRegex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}")]
    private static partial Regex EmailRegex();
}
