using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using JobFinder.Models;

namespace JobFinder.Services;

public partial class UpworkService : IUpworkService, IAsyncDisposable
{
    private readonly ISettingsService _settingsService;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private bool _isLoggedIn;
    private readonly string _userDataDir;
    private readonly Random _random = new();

    // Upwork URLs
    private const string UpworkBaseUrl = "https://www.upwork.com";
    private const string UpworkLoginUrl = "https://www.upwork.com/ab/account-security/login";
    private const string UpworkFeedUrl = "https://www.upwork.com/nx/find-work/best-matches";
    private const string UpworkSearchUrl = "https://www.upwork.com/nx/search/jobs";

    public JobPlatform Platform => JobPlatform.Upwork;
    public bool IsLoggedIn => _isLoggedIn;
    public bool IsBrowserOpen => _browser != null;

    public UpworkService(ISettingsService settingsService)
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

        // Use persistent context with user data directory to avoid bot detection
        // This makes the browser behave like a real user's Chrome installation
        var upworkUserDataDir = Path.Combine(_userDataDir, "upwork-profile");
        Directory.CreateDirectory(upworkUserDataDir);

        var browserArgs = new List<string>
        {
            "--disable-blink-features=AutomationControlled",
            "--disable-infobars",
            "--no-first-run",
            "--no-default-browser-check"
        };

        if (_settingsService.Settings.StartBrowserMinimized)
            browserArgs.Add("--start-minimized");
        else
            browserArgs.Add("--start-maximized");

        // Use persistent context - this stores all browser data including cookies,
        // local storage, and browser fingerprint in a real Chrome profile
        _context = await _playwright!.Chromium.LaunchPersistentContextAsync(
            upworkUserDataDir,
            new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = false,
                Channel = "chrome", // Use system Chrome instead of Playwright's Chromium
                Args = browserArgs,
                ViewportSize = ViewportSize.NoViewport,
                IgnoreDefaultArgs = new[] { "--enable-automation" }
            });

        _page = _context.Pages.Count > 0 ? _context.Pages[0] : await _context.NewPageAsync();

        // Check if we have an existing session by navigating to feed
        await _page.GotoAsync(UpworkFeedUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        // Wait for page to stabilize and check URL
        await Task.Delay(2000);

        var currentUrl = _page.Url;
        if (currentUrl.Contains("/login") || currentUrl.Contains("/ab/account-security"))
        {
            // Not logged in, stay on login page for user to authenticate
            _isLoggedIn = false;
        }
    }

    public async Task<bool> CheckLoginStatusAsync()
    {
        if (_page == null) return false;

        try
        {
            // Wait for page to stabilize
            await Task.Delay(1000);

            var currentUrl = _page.Url;

            // If we're on the login page, we're not logged in
            if (currentUrl.Contains("/login") || currentUrl.Contains("/ab/account-security"))
            {
                _isLoggedIn = false;
                return false;
            }

            // Try multiple selectors for user avatar/menu in navigation
            // Upwork uses various elements depending on the page
            var avatarSelectors = new[]
            {
                "[data-test='nav-user-avatar']",
                "[data-qa='user-avatar']",
                ".nav-avatar",
                ".up-avatar",
                "button[aria-label*='account']",
                ".nav-d-user-menu",
                "[data-cy='nav-user-menu']",
                "img[alt*='avatar' i]",
                ".air3-avatar"
            };

            var hasUserMenu = false;
            foreach (var selector in avatarSelectors)
            {
                try
                {
                    var count = await _page.Locator(selector).CountAsync();
                    if (count > 0)
                    {
                        hasUserMenu = true;
                        break;
                    }
                }
                catch { /* Selector not found, try next */ }
            }

            // Check for login/signup buttons that indicate NOT logged in
            var notLoggedInSelectors = new[]
            {
                "a[href*='/ab/account-security/login']",
                "a[data-qa='login']",
                "button:has-text('Log In')",
                "a:has-text('Log In')",
                "[data-test='login-link']"
            };

            var hasLoginButton = false;
            foreach (var selector in notLoggedInSelectors)
            {
                try
                {
                    var count = await _page.Locator(selector).CountAsync();
                    if (count > 0)
                    {
                        hasLoginButton = true;
                        break;
                    }
                }
                catch { /* Selector not found, try next */ }
            }

            // Check if we're on a page that typically requires login
            var isOnAuthenticatedPage = currentUrl.Contains("/nx/find-work") ||
                                        currentUrl.Contains("/freelancers/~") ||
                                        currentUrl.Contains("/ab/proposals") ||
                                        currentUrl.Contains("/nx/search/jobs") ||
                                        currentUrl.Contains("/messages");

            _isLoggedIn = (hasUserMenu || isOnAuthenticatedPage) && !hasLoginButton;

            // With persistent context, session data is automatically saved to user data directory
            return _isLoggedIn;
        }
        catch
        {
            _isLoggedIn = false;
            return false;
        }
    }

    public async Task<List<Job>> SearchJobsAsync(
        SearchFilter filter,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var jobs = new List<Job>();

        if (_page == null || !_isLoggedIn)
        {
            progress?.Report("Not logged in to Upwork");
            return jobs;
        }

        try
        {
            progress?.Report("Building Upwork search URL...");

            // Build search URL with filters
            var searchUrl = BuildSearchUrl(filter);
            progress?.Report($"Navigating to: {searchUrl}");

            await HumanDelayAsync();
            await _page.GotoAsync(searchUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await HumanDelayAsync();

            cancellationToken.ThrowIfCancellationRequested();

            // Wait for job cards to load - try multiple selectors for Upwork's UI
            progress?.Report("Waiting for job listings to load...");

            // Upwork uses various selectors depending on the page version
            var jobListSelectors = new[]
            {
                "article[data-test='JobTile']",
                "[data-test='job-tile-list'] article",
                "section.air3-card-section",
                "[data-ev-label='job_tile']",
                ".job-tile-list article",
                ".up-card-section"
            };

            string? workingSelector = null;
            foreach (var selector in jobListSelectors)
            {
                try
                {
                    var count = await _page.Locator(selector).CountAsync();
                    if (count > 0)
                    {
                        workingSelector = selector;
                        progress?.Report($"Found {count} jobs using selector: {selector}");
                        break;
                    }
                }
                catch { /* Try next selector */ }
            }

            if (workingSelector == null)
            {
                // Try waiting for any content to load
                await Task.Delay(3000);

                // Debug: Log the page content structure
                var pageContent = await _page.ContentAsync();
                var hasJobs = pageContent.Contains("job") || pageContent.Contains("Job");
                progress?.Report($"Page loaded, contains 'job': {hasJobs}. URL: {_page.Url}");

                // Last resort: try finding any clickable job links
                var jobLinks = await _page.Locator("a[href*='/jobs/']").CountAsync();
                progress?.Report($"Found {jobLinks} job links on page");

                if (jobLinks == 0)
                {
                    progress?.Report("No jobs found - page structure may have changed");
                    return jobs;
                }

                // Use job links as fallback
                workingSelector = "a[href*='/jobs/']";
            }

            int totalScraped = 0;
            int pageNumber = 1;
            int maxPages = (filter.MaxResults / 10) + 1; // Upwork shows ~10 jobs per page

            while (totalScraped < filter.MaxResults && pageNumber <= maxPages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report($"Scraping page {pageNumber}...");

                // Get all job cards on current page using the working selector
                var jobCards = await _page.Locator(workingSelector).AllAsync();

                if (jobCards.Count == 0)
                {
                    progress?.Report("No more jobs found");
                    break;
                }

                foreach (var card in jobCards)
                {
                    if (totalScraped >= filter.MaxResults)
                        break;

                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var job = await ParseJobCardAsync(card);
                        if (job != null)
                        {
                            jobs.Add(job);
                            totalScraped++;
                            progress?.Report($"Found: {job.Title}");
                        }
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"Error parsing job card: {ex.Message}");
                    }
                }

                // Check for next page
                if (totalScraped < filter.MaxResults)
                {
                    var nextButton = _page.Locator("button[data-test='pagination-next'], a:has-text('Next')").First;
                    var hasNextPage = await nextButton.CountAsync() > 0 && await nextButton.IsEnabledAsync();

                    if (hasNextPage)
                    {
                        await HumanDelayAsync();
                        await nextButton.ClickAsync();
                        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                        await HumanDelayAsync();
                        pageNumber++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            progress?.Report($"Found {jobs.Count} jobs on Upwork");
        }
        catch (OperationCanceledException)
        {
            progress?.Report("Search cancelled");
            throw;
        }
        catch (Exception ex)
        {
            progress?.Report($"Error searching Upwork: {ex.Message}");
        }

        return jobs;
    }

    private string BuildSearchUrl(SearchFilter filter)
    {
        var queryParams = new List<string>();

        // Search query (job title)
        if (!string.IsNullOrEmpty(filter.JobTitle))
        {
            queryParams.Add($"q={Uri.EscapeDataString(filter.JobTitle)}");
        }

        // Sort by recency
        queryParams.Add("sort=recency");

        // Add .NET specific search terms if not already in title
        var searchTerms = filter.JobTitle?.ToLower() ?? "";
        if (!searchTerms.Contains(".net") && !searchTerms.Contains("dotnet"))
        {
            // Search specifically for .NET jobs
            queryParams.Clear();
            queryParams.Add($"q={Uri.EscapeDataString(filter.JobTitle + " .NET")}");
            queryParams.Add("sort=recency");
        }

        // Experience level mapping
        if (filter.ExperienceLevels?.Any() == true)
        {
            foreach (var level in filter.ExperienceLevels)
            {
                var upworkLevel = MapExperienceLevel(level);
                if (!string.IsNullOrEmpty(upworkLevel))
                {
                    queryParams.Add($"contractor_tier={upworkLevel}");
                }
            }
        }

        // Location/remote filter
        if (filter.RemoteOnly)
        {
            // Upwork jobs are inherently remote, no special filter needed
        }

        return $"{UpworkSearchUrl}?{string.Join("&", queryParams)}";
    }

    private static string? MapExperienceLevel(string linkedInLevel)
    {
        return linkedInLevel.ToLower() switch
        {
            "entry level" or "internship" => "1", // Entry level
            "associate" or "mid-senior level" => "2", // Intermediate
            "senior" or "director" or "executive" => "3", // Expert
            _ => null
        };
    }

    private async Task<Job?> ParseJobCardAsync(ILocator card)
    {
        try
        {
            string href = "";
            string title = "";

            // First, try to get href - check if the card itself is a link or contains a link
            var tagName = await card.EvaluateAsync<string>("el => el.tagName.toLowerCase()");

            if (tagName == "a")
            {
                // The card itself is a link element
                href = await card.GetAttributeAsync("href") ?? "";
                title = await card.TextContentAsync() ?? "";
            }
            else
            {
                // Try to find a job link inside the card
                var linkSelectors = new[]
                {
                    "a[href*='/jobs/~']",
                    "a[href*='/ab/proposals/job/']",
                    "h2 a",
                    "h3 a",
                    ".job-tile-title a",
                    "[data-test='job-tile-title'] a",
                    "a.up-n-link"
                };

                foreach (var selector in linkSelectors)
                {
                    try
                    {
                        var link = card.Locator(selector).First;
                        if (await link.CountAsync() > 0)
                        {
                            href = await link.GetAttributeAsync("href") ?? "";
                            title = await link.TextContentAsync() ?? "";
                            if (!string.IsNullOrEmpty(href))
                                break;
                        }
                    }
                    catch { /* Try next selector */ }
                }
            }

            // Extract job ID from URL
            var jobId = ExtractJobIdFromUrl(href);
            if (string.IsNullOrEmpty(jobId))
                return null;

            title = title.Trim();
            if (string.IsNullOrEmpty(title))
                title = "Untitled Job";

            // Try to get description
            var description = "";
            var descSelectors = new[] { "[data-test='job-description-text']", ".job-description", "p.mb-0", ".up-line-clamp" };
            foreach (var selector in descSelectors)
            {
                try
                {
                    var elem = card.Locator(selector).First;
                    if (await elem.CountAsync() > 0)
                    {
                        description = await elem.TextContentAsync() ?? "";
                        if (!string.IsNullOrEmpty(description))
                            break;
                    }
                }
                catch { /* Try next */ }
            }

            // Try to get posted time
            var datePosted = DateTime.Now;
            var postedSelectors = new[] { "[data-test='posted-on']", "small:has-text('Posted')", "span:has-text('ago')" };
            foreach (var selector in postedSelectors)
            {
                try
                {
                    var elem = card.Locator(selector).First;
                    if (await elem.CountAsync() > 0)
                    {
                        var postedText = await elem.TextContentAsync() ?? "";
                        datePosted = ParsePostedDate(postedText);
                        break;
                    }
                }
                catch { /* Try next */ }
            }

            // Budget/Rate (with error handling)
            var (budgetType, hourlyMin, hourlyMax, fixedPrice) = ("", (decimal?)null, (decimal?)null, (decimal?)null);
            try
            {
                (budgetType, hourlyMin, hourlyMax, fixedPrice) = await ParseBudgetAsync(card);
            }
            catch { /* Use defaults */ }

            // Client info (with error handling)
            var (clientRating, clientSpent, clientHireRate) = ((decimal?)null, (decimal?)null, (decimal?)null);
            try
            {
                (clientRating, clientSpent, clientHireRate) = await ParseClientInfoAsync(card);
            }
            catch { /* Use defaults */ }

            // Proposals count
            int? proposalsCount = null;
            var proposalSelectors = new[] { "[data-test='proposals']", "span:has-text('Proposals')", "span:has-text('proposals')" };
            foreach (var selector in proposalSelectors)
            {
                try
                {
                    var elem = card.Locator(selector).First;
                    if (await elem.CountAsync() > 0)
                    {
                        var text = await elem.TextContentAsync() ?? "";
                        proposalsCount = ParseProposalsCount(text);
                        break;
                    }
                }
                catch { /* Try next */ }
            }

            // Skills/tags
            var skills = new List<string>();
            var skillSelectors = new[] { "[data-test='token']", ".up-skill-badge", ".air3-token", "span.badge" };
            foreach (var selector in skillSelectors)
            {
                try
                {
                    var skillElements = await card.Locator(selector).AllAsync();
                    if (skillElements.Count > 0)
                    {
                        foreach (var skill in skillElements)
                        {
                            var skillText = await skill.TextContentAsync();
                            if (!string.IsNullOrEmpty(skillText))
                                skills.Add(skillText.Trim());
                        }
                        break;
                    }
                }
                catch { /* Try next */ }
            }

            // Duration
            var duration = "";
            var durationSelectors = new[] { "[data-test='duration']", "span:has-text('Est. time')", "span:has-text('month')" };
            foreach (var selector in durationSelectors)
            {
                try
                {
                    var elem = card.Locator(selector).First;
                    if (await elem.CountAsync() > 0)
                    {
                        duration = await elem.TextContentAsync() ?? "";
                        break;
                    }
                }
                catch { /* Try next */ }
            }

            // Connects required
            int? connects = null;
            try
            {
                var connectsElem = card.Locator("span:has-text('Connects')").First;
                if (await connectsElem.CountAsync() > 0)
                {
                    var connectsText = await connectsElem.TextContentAsync() ?? "";
                    connects = ParseConnectsRequired(connectsText);
                }
            }
            catch { /* Use default */ }

            var job = new Job
            {
                Platform = JobPlatform.Upwork,
                ExternalJobId = jobId,
                Title = title,
                ScrapedCompanyName = "Upwork Client",
                Location = "Remote",
                Description = description.Trim(),
                JobUrl = href.StartsWith("http") ? href : $"{UpworkBaseUrl}{href}",
                HasEasyApply = true,
                DatePosted = datePosted,
                DateScraped = DateTime.Now,
                Status = ApplicationStatus.New,

                // Upwork-specific fields
                BudgetType = budgetType,
                HourlyRateMin = hourlyMin,
                HourlyRateMax = hourlyMax,
                FixedPriceBudget = fixedPrice,
                ProjectDuration = duration.Trim(),
                ClientRating = clientRating,
                ClientTotalSpent = clientSpent,  // decimal? from ParseClientInfoAsync
                ClientHireRate = clientHireRate,
                ProposalsCount = proposalsCount,
                ConnectsRequired = connects,
                RequiredSkills = skills
            };

            return job;
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractJobIdFromUrl(string url)
    {
        // Upwork job URLs look like: /jobs/~01234567890abcdef or /ab/proposals/job/~01234567890abcdef/apply
        var match = Regex.Match(url, @"~([a-zA-Z0-9]+)");
        return match.Success ? match.Groups[1].Value : "";
    }

    private static DateTime ParsePostedDate(string postedText)
    {
        postedText = postedText.ToLower().Trim();

        if (postedText.Contains("just now") || postedText.Contains("moments ago"))
            return DateTime.Now;

        if (postedText.Contains("minute"))
        {
            var match = Regex.Match(postedText, @"(\d+)\s*minute");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var minutes))
                return DateTime.Now.AddMinutes(-minutes);
        }

        if (postedText.Contains("hour"))
        {
            var match = Regex.Match(postedText, @"(\d+)\s*hour");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var hours))
                return DateTime.Now.AddHours(-hours);
        }

        if (postedText.Contains("day"))
        {
            var match = Regex.Match(postedText, @"(\d+)\s*day");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var days))
                return DateTime.Now.AddDays(-days);
        }

        if (postedText.Contains("week"))
        {
            var match = Regex.Match(postedText, @"(\d+)\s*week");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var weeks))
                return DateTime.Now.AddDays(-weeks * 7);
        }

        if (postedText.Contains("month"))
        {
            var match = Regex.Match(postedText, @"(\d+)\s*month");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var months))
                return DateTime.Now.AddMonths(-months);
        }

        return DateTime.Now;
    }

    private async Task<(string? budgetType, decimal? hourlyMin, decimal? hourlyMax, decimal? fixedPrice)> ParseBudgetAsync(ILocator card)
    {
        try
        {
            var budgetElement = card.Locator("[data-test='budget'], [data-test='is-fixed-price'], .js-budget, strong:has-text('$')").First;
            var budgetText = await budgetElement.TextContentAsync() ?? "";
            budgetText = budgetText.Trim();

            // Fixed price: "$500" or "$1,000"
            if (budgetText.Contains("Fixed") || !budgetText.Contains("/hr"))
            {
                var match = Regex.Match(budgetText, @"\$[\d,]+(?:\.\d+)?");
                if (match.Success)
                {
                    var value = decimal.Parse(match.Value.Replace("$", "").Replace(",", ""));
                    return ("Fixed", null, null, value);
                }
            }

            // Hourly: "$25.00-$50.00 /hr" or "$30.00 /hr"
            if (budgetText.Contains("/hr"))
            {
                var matches = Regex.Matches(budgetText, @"\$[\d,]+(?:\.\d+)?");
                if (matches.Count >= 2)
                {
                    var min = decimal.Parse(matches[0].Value.Replace("$", "").Replace(",", ""));
                    var max = decimal.Parse(matches[1].Value.Replace("$", "").Replace(",", ""));
                    return ("Hourly", min, max, null);
                }
                else if (matches.Count == 1)
                {
                    var rate = decimal.Parse(matches[0].Value.Replace("$", "").Replace(",", ""));
                    return ("Hourly", rate, null, null);
                }
            }
        }
        catch { }

        return (null, null, null, null);
    }

    private async Task<(decimal? rating, decimal? spent, decimal? hireRate)> ParseClientInfoAsync(ILocator card)
    {
        decimal? rating = null;
        decimal? spent = null;
        decimal? hireRate = null;

        try
        {
            // Client rating
            var ratingElement = card.Locator("[data-test='client-rating'], .client-rating").First;
            var ratingText = await ratingElement.TextContentAsync() ?? "";
            var ratingMatch = Regex.Match(ratingText, @"(\d+\.?\d*)");
            if (ratingMatch.Success)
                rating = decimal.Parse(ratingMatch.Groups[1].Value);

            // Client total spent
            var spentElement = card.Locator("[data-test='total-spent'], .client-spent").First;
            var spentText = await spentElement.TextContentAsync() ?? "";
            var spentMatch = Regex.Match(spentText, @"\$[\d,]+(?:\.\d+)?([KMB])?");
            if (spentMatch.Success)
            {
                var value = decimal.Parse(spentMatch.Value.Replace("$", "").Replace(",", "").Replace("K", "").Replace("M", "").Replace("B", ""));
                if (spentText.Contains("K")) value *= 1000;
                if (spentText.Contains("M")) value *= 1000000;
                if (spentText.Contains("B")) value *= 1000000000;
                spent = value;
            }

            // Client hire rate
            var hireRateElement = card.Locator("[data-test='hire-rate'], .client-hire-rate").First;
            var hireRateText = await hireRateElement.TextContentAsync() ?? "";
            var hireRateMatch = Regex.Match(hireRateText, @"(\d+)%");
            if (hireRateMatch.Success)
                hireRate = decimal.Parse(hireRateMatch.Groups[1].Value);
        }
        catch { }

        return (rating, spent, hireRate);
    }

    private static int? ParseProposalsCount(string text)
    {
        // "5 to 10" or "Less than 5" or "10 to 15" or "50+"
        text = text.ToLower();

        if (text.Contains("less than 5") || text.Contains("0 to 5"))
            return 2; // Approximate

        var match = Regex.Match(text, @"(\d+)\s*to\s*(\d+)");
        if (match.Success)
        {
            var min = int.Parse(match.Groups[1].Value);
            var max = int.Parse(match.Groups[2].Value);
            return (min + max) / 2; // Return average
        }

        match = Regex.Match(text, @"(\d+)\+?");
        if (match.Success)
            return int.Parse(match.Groups[1].Value);

        return null;
    }

    private static int? ParseConnectsRequired(string text)
    {
        var match = Regex.Match(text, @"(\d+)\s*connects", RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    public async Task<JobDetails?> GetJobDetailsAsync(string jobUrl)
    {
        if (_page == null || !_isLoggedIn)
            return null;

        try
        {
            await HumanDelayAsync();
            await _page.GotoAsync(jobUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await HumanDelayAsync();

            // Wait for job details to load
            await _page.WaitForSelectorAsync("[data-test='job-description'], .job-description, .up-card-section",
                new PageWaitForSelectorOptions { Timeout = 10000 });

            var details = new JobDetails();

            // Full description
            var descElement = _page.Locator("[data-test='job-description'], .job-description").First;
            details.Description = await descElement.TextContentAsync() ?? "";
            details.Description = details.Description.Trim();

            // Upwork always allows proposals (their version of "Easy Apply")
            details.HasEasyApply = true;

            // Budget info
            var budgetElement = _page.Locator("[data-test='budget'], [data-test='hourly-rate']").First;
            var budgetText = await budgetElement.TextContentAsync() ?? "";

            if (budgetText.Contains("/hr"))
            {
                var matches = Regex.Matches(budgetText, @"\$[\d,]+(?:\.\d+)?");
                if (matches.Count >= 2)
                {
                    details.HourlyRateMin = decimal.Parse(matches[0].Value.Replace("$", "").Replace(",", ""));
                    details.HourlyRateMax = decimal.Parse(matches[1].Value.Replace("$", "").Replace(",", ""));
                }
                else if (matches.Count == 1)
                {
                    details.HourlyRateMin = decimal.Parse(matches[0].Value.Replace("$", "").Replace(",", ""));
                }
            }
            else
            {
                var match = Regex.Match(budgetText, @"\$[\d,]+(?:\.\d+)?");
                if (match.Success)
                {
                    details.FixedPrice = decimal.Parse(match.Value.Replace("$", "").Replace(",", ""));
                }
            }

            // Client info
            var clientSection = _page.Locator("[data-test='client-info'], .client-info").First;
            if (await clientSection.CountAsync() > 0)
            {
                var clientText = await clientSection.TextContentAsync() ?? "";

                // Rating
                var ratingMatch = Regex.Match(clientText, @"(\d+\.?\d*)\s*(?:star|rating)", RegexOptions.IgnoreCase);
                if (ratingMatch.Success)
                    details.ClientRating = decimal.Parse(ratingMatch.Groups[1].Value);

                // Total spent
                var spentMatch = Regex.Match(clientText, @"\$[\d,]+(?:\.\d+)?([KMB])?");
                if (spentMatch.Success)
                {
                    var value = decimal.Parse(spentMatch.Value.Replace("$", "").Replace(",", "").Replace("K", "").Replace("M", "").Replace("B", ""));
                    if (clientText.Contains("K")) value *= 1000;
                    if (clientText.Contains("M")) value *= 1000000;
                    details.ClientTotalSpent = value;
                }

                // Hire rate
                var hireMatch = Regex.Match(clientText, @"(\d+)%\s*hire\s*rate", RegexOptions.IgnoreCase);
                if (hireMatch.Success)
                    details.ClientHireRate = decimal.Parse(hireMatch.Groups[1].Value);
            }

            // Proposals count
            var proposalsElement = _page.Locator("[data-test='proposals'], span:has-text('Proposals')").First;
            var proposalsText = await proposalsElement.TextContentAsync() ?? "";
            details.ProposalsCount = ParseProposalsCount(proposalsText);

            // Required skills
            var skillElements = await _page.Locator("[data-test='token'], .up-skill-badge, .skill-badge").AllAsync();
            details.RequiredSkills = new List<string>();
            foreach (var skill in skillElements)
            {
                var skillText = await skill.TextContentAsync();
                if (!string.IsNullOrEmpty(skillText))
                    details.RequiredSkills.Add(skillText.Trim());
            }

            // Project duration
            var durationElement = _page.Locator("[data-test='duration'], span:has-text('Duration')").First;
            details.ProjectDuration = await durationElement.TextContentAsync();

            // Experience level
            var expElement = _page.Locator("[data-test='experience-level'], span:has-text('Experience')").First;
            details.ExperienceLevel = await expElement.TextContentAsync();

            return details;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ApplicationSession?> PrepareApplicationAsync(
        Job job,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_page == null || !_isLoggedIn)
        {
            progress?.Report("Browser not ready or not logged in");
            return null;
        }

        var session = new ApplicationSession
        {
            JobId = job.Id,
            Platform = JobPlatform.Upwork,
            ExternalJobId = job.ExternalJobId,
            JobTitle = job.Title,
            Company = job.Company?.Name ?? job.ScrapedCompanyName ?? "Upwork Client",
            JobUrl = job.JobUrl ?? ""
        };

        try
        {
            // Navigate to job page
            progress?.Report("Opening job page...");
            session.LogAction("Navigate", $"Opening {job.JobUrl}");

            await HumanDelayAsync();
            await _page.GotoAsync(job.JobUrl!, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await HumanDelayAsync();

            cancellationToken.ThrowIfCancellationRequested();

            // Click "Apply Now" or "Submit Proposal" button
            progress?.Report("Looking for Apply button...");
            var applyButton = _page.Locator("button:has-text('Apply Now'), button:has-text('Submit a Proposal'), a:has-text('Apply Now')").First;

            if (await applyButton.CountAsync() == 0)
            {
                session.Status = ApplicationSessionStatus.Failed;
                session.ErrorMessage = "Could not find Apply button. You may have already applied to this job.";
                return session;
            }

            await HumanDelayAsync();
            await applyButton.ClickAsync();
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await HumanDelayAsync();

            cancellationToken.ThrowIfCancellationRequested();

            // Wait for proposal form to load
            progress?.Report("Waiting for proposal form...");
            try
            {
                await _page.WaitForSelectorAsync("[data-test='cover-letter'], textarea[name='coverLetter'], .proposal-form",
                    new PageWaitForSelectorOptions { Timeout = 10000 });
            }
            catch (TimeoutException)
            {
                session.Status = ApplicationSessionStatus.Failed;
                session.ErrorMessage = "Proposal form did not load";
                return session;
            }

            // Detect form fields
            progress?.Report("Detecting form fields...");

            var questions = new List<EasyApplyQuestion>();

            // Cover letter (always present)
            var coverLetterField = _page.Locator("[data-test='cover-letter'] textarea, textarea[name='coverLetter'], textarea.cover-letter").First;
            if (await coverLetterField.CountAsync() > 0)
            {
                questions.Add(new EasyApplyQuestion
                {
                    QuestionText = "Cover Letter",
                    Type = QuestionType.TextArea,
                    IsRequired = true,
                    Selector = "[data-test='cover-letter'] textarea, textarea[name='coverLetter']"
                });
            }

            // Bid amount (hourly rate or fixed price)
            var bidField = _page.Locator("[data-test='bid-input'] input, input[name='rate'], input[name='amount']").First;
            if (await bidField.CountAsync() > 0)
            {
                var bidLabel = job.BudgetType == "Hourly" ? "Your Hourly Rate ($)" : "Your Bid Amount ($)";
                questions.Add(new EasyApplyQuestion
                {
                    QuestionText = bidLabel,
                    Type = QuestionType.Number,
                    IsRequired = true,
                    Selector = "[data-test='bid-input'] input, input[name='rate'], input[name='amount']"
                });
            }

            // Screening questions from the client
            var screeningQuestions = await _page.Locator("[data-test='question'], .screening-question, .additional-question").AllAsync();
            foreach (var question in screeningQuestions)
            {
                var labelElement = question.Locator("label, .question-text").First;
                var questionText = await labelElement.TextContentAsync() ?? "Additional Question";

                var inputField = question.Locator("input, textarea, select").First;
                var tagName = await inputField.EvaluateAsync<string>("el => el.tagName.toLowerCase()");

                var questionType = tagName switch
                {
                    "textarea" => QuestionType.TextArea,
                    "select" => QuestionType.Select,
                    _ => QuestionType.Text
                };

                questions.Add(new EasyApplyQuestion
                {
                    QuestionText = questionText.Trim(),
                    Type = questionType,
                    IsRequired = true
                });
            }

            session.Questions = questions;
            session.TotalPages = 1; // Upwork typically has single-page proposals
            session.Status = ApplicationSessionStatus.ReadyForReview;

            progress?.Report($"Found {questions.Count} fields to fill");
            session.LogAction("DetectFields", $"Found {questions.Count} fields");

            return session;
        }
        catch (OperationCanceledException)
        {
            session.Status = ApplicationSessionStatus.Cancelled;
            throw;
        }
        catch (Exception ex)
        {
            session.Status = ApplicationSessionStatus.Failed;
            session.ErrorMessage = ex.Message;
            session.LogAction("Error", ex.Message, false);
            return session;
        }
    }

    public async Task<bool> SubmitApplicationAsync(
        ApplicationSession session,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_page == null || !_isLoggedIn)
        {
            progress?.Report("Browser not ready");
            return false;
        }

        try
        {
            session.Status = ApplicationSessionStatus.Submitting;

            // Fill in cover letter
            progress?.Report("Filling cover letter...");
            var coverLetterField = _page.Locator("[data-test='cover-letter'] textarea, textarea[name='coverLetter'], textarea.cover-letter").First;
            if (await coverLetterField.CountAsync() > 0 && !string.IsNullOrEmpty(session.ApplicationMessage))
            {
                await coverLetterField.ClearAsync();
                await HumanTypeAsync(coverLetterField, session.ApplicationMessage);
                session.LogAction("Fill", "Filled cover letter");
            }

            // Fill in bid amount if present
            var bidField = _page.Locator("[data-test='bid-input'] input, input[name='rate'], input[name='amount']").First;
            if (await bidField.CountAsync() > 0)
            {
                // Find the bid answer from questions
                var bidQuestion = session.Questions.FirstOrDefault(q =>
                    q.QuestionText.Contains("Rate") || q.QuestionText.Contains("Bid") || q.QuestionText.Contains("Amount"));
                if (bidQuestion?.Answer != null)
                {
                    await bidField.ClearAsync();
                    await bidField.FillAsync(bidQuestion.Answer);
                    session.LogAction("Fill", $"Set bid amount: {bidQuestion.Answer}");
                }
            }

            // Fill screening questions
            foreach (var question in session.Questions.Where(q => !string.IsNullOrEmpty(q.Selector)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(question.Answer))
                    continue;

                try
                {
                    var field = _page.Locator(question.Selector!).First;
                    if (await field.CountAsync() > 0)
                    {
                        await field.ClearAsync();
                        if (question.Type == QuestionType.TextArea)
                        {
                            await HumanTypeAsync(field, question.Answer);
                        }
                        else
                        {
                            await field.FillAsync(question.Answer);
                        }
                        session.LogAction("Fill", $"Answered: {question.QuestionText}");
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report($"Warning: Could not fill field - {ex.Message}");
                }
            }

            await HumanDelayAsync();
            cancellationToken.ThrowIfCancellationRequested();

            // Click Submit button
            progress?.Report("Submitting proposal...");
            var submitButton = _page.Locator("button[type='submit']:has-text('Submit'), button:has-text('Submit Proposal'), button:has-text('Send')").First;

            if (await submitButton.CountAsync() == 0)
            {
                session.Status = ApplicationSessionStatus.Failed;
                session.ErrorMessage = "Could not find Submit button";
                return false;
            }

            await submitButton.ClickAsync();
            session.LogAction("Submit", "Clicked submit button");

            // Wait for confirmation
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000);

            // Check for success indicators
            var successIndicator = _page.Locator("[data-test='proposal-submitted'], .success-message, h1:has-text('submitted')").First;
            var errorIndicator = _page.Locator("[data-test='error-message'], .error-message, .alert-danger").First;

            if (await errorIndicator.CountAsync() > 0)
            {
                var errorText = await errorIndicator.TextContentAsync();
                session.Status = ApplicationSessionStatus.Failed;
                session.ErrorMessage = errorText;
                return false;
            }

            session.Status = ApplicationSessionStatus.Submitted;
            session.CompletedAt = DateTime.Now;
            progress?.Report("Proposal submitted successfully!");

            return true;
        }
        catch (OperationCanceledException)
        {
            session.Status = ApplicationSessionStatus.Cancelled;
            throw;
        }
        catch (Exception ex)
        {
            session.Status = ApplicationSessionStatus.Failed;
            session.ErrorMessage = ex.Message;
            return false;
        }
    }

    public async Task CancelApplicationAsync()
    {
        if (_page == null) return;

        try
        {
            // Try to close any open modals
            var closeButton = _page.Locator("[data-test='modal-close'], button[aria-label='Close'], .modal-close").First;
            if (await closeButton.CountAsync() > 0)
            {
                await closeButton.ClickAsync();
            }

            // Navigate away from proposal page
            await _page.GoBackAsync();
        }
        catch { }
    }

    public async Task<int?> GetConnectsBalanceAsync()
    {
        if (_page == null || !_isLoggedIn)
            return null;

        try
        {
            // Navigate to a page that shows Connects balance
            var connectsElement = _page.Locator("[data-test='connects-balance'], .connects-balance, span:has-text('Connects')").First;

            if (await connectsElement.CountAsync() > 0)
            {
                var text = await connectsElement.TextContentAsync() ?? "";
                var match = Regex.Match(text, @"(\d+)");
                if (match.Success)
                    return int.Parse(match.Groups[1].Value);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> HasEnoughConnectsAsync(int connectsRequired)
    {
        var balance = await GetConnectsBalanceAsync();
        return balance.HasValue && balance.Value >= connectsRequired;
    }

    public async Task CloseAsync()
    {
        // With persistent context, all data is automatically saved to the user data directory
        // No need to manually save storage state

        if (_context != null)
        {
            await _context.CloseAsync();
            _context = null;
        }

        // Browser is managed by persistent context, no separate browser instance
        _browser = null;
        _page = null;
        _isLoggedIn = false;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _playwright?.Dispose();
    }

    private string GetStorageStatePath()
    {
        return Path.Combine(_userDataDir, "upwork-storage-state.json");
    }

    private async Task HumanDelayAsync()
    {
        var minDelay = _settingsService.Settings.MinActionDelayMs;
        var maxDelay = _settingsService.Settings.MaxActionDelayMs;
        var actualDelay = _random.Next(minDelay, maxDelay);
        await Task.Delay(Math.Max(100, actualDelay));
    }

    private async Task HumanTypeAsync(ILocator element, string text)
    {
        foreach (var c in text)
        {
            await element.PressAsync(c.ToString());
            await Task.Delay(_random.Next(30, 100));
        }
    }
}
