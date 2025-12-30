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
    private readonly Random _random = new();

    public JobPlatform Platform => JobPlatform.LinkedIn;
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
                            Platform = JobPlatform.LinkedIn,
                            ExternalJobId = jobId,
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
            Platform = JobPlatform.LinkedIn,
            ExternalJobId = jobId,
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

    #region Auto-Apply Implementation

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
            Platform = JobPlatform.LinkedIn,
            ExternalJobId = job.ExternalJobId,
            JobTitle = job.Title,
            Company = job.Company?.Name ?? job.ScrapedCompanyName ?? "Unknown",
            JobUrl = job.JobUrl ?? ""
        };

        try
        {
            // Step 1: Navigate to job page with human-like delay
            progress?.Report("Opening job page...");
            session.LogAction("Navigate", $"Opening {job.JobUrl}");

            await HumanDelayAsync();
            await _page.GotoAsync(job.JobUrl!, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await HumanDelayAsync();

            // Step 2: Find and click Easy Apply button
            progress?.Report("Looking for Easy Apply button...");
            var easyApplyButton = await FindEasyApplyButtonAsync();

            if (easyApplyButton == null)
            {
                session.Status = ApplicationSessionStatus.Failed;
                session.ErrorMessage = "Easy Apply button not found";
                session.LogAction("FindButton", "Easy Apply button not found", false);
                return session;
            }

            session.LogAction("FindButton", "Found Easy Apply button");

            // Step 3: Click Easy Apply button
            progress?.Report("Opening Easy Apply form...");
            await HumanDelayAsync(500, 1500);
            await easyApplyButton.ClickAsync();
            await HumanDelayAsync(1000, 2000);

            // Step 4: Wait for modal to appear
            try
            {
                await _page.WaitForSelectorAsync(
                    ".jobs-easy-apply-modal, .jobs-easy-apply-content, [data-test-modal-id='easy-apply-modal']",
                    new PageWaitForSelectorOptions { Timeout = 5000 });
                session.LogAction("ModalOpen", "Easy Apply modal opened");
            }
            catch (TimeoutException)
            {
                session.Status = ApplicationSessionStatus.Failed;
                session.ErrorMessage = "Easy Apply modal did not appear";
                session.LogAction("ModalOpen", "Modal timeout", false);
                return session;
            }

            // Step 5: Detect all form fields and questions across all pages
            progress?.Report("Analyzing application form...");
            var (questions, totalPages) = await DetectAllQuestionsAsync(progress, cancellationToken);

            session.Questions = questions;
            session.TotalPages = totalPages;
            session.LogAction("FormAnalyzed", $"Detected {questions.Count} questions across {totalPages} pages");

            // Step 6: Navigate back to first page for filling
            await NavigateToFirstPageAsync();

            session.Status = ApplicationSessionStatus.ReadyForReview;
            progress?.Report($"Form ready: {questions.Count} questions detected");

            return session;
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
            session.ErrorMessage = "Browser not ready";
            return false;
        }

        try
        {
            session.Status = ApplicationSessionStatus.Submitting;
            var currentPage = 0;
            var questionsOnCurrentPage = session.Questions.Where(q => q.PageIndex == currentPage).ToList();

            while (!cancellationToken.IsCancellationRequested)
            {
                // Fill all questions on current page
                foreach (var question in questionsOnCurrentPage)
                {
                    if (question.IsPreFilled && !string.IsNullOrEmpty(question.PreFilledValue))
                    {
                        session.LogAction("Skip", $"Pre-filled: {question.QuestionText}");
                        continue;
                    }

                    progress?.Report($"Filling: {Truncate(question.QuestionText, 40)}...");
                    await HumanDelayAsync(500, 1200);

                    var success = await FillQuestionAsync(question);
                    session.LogAction("Fill", question.QuestionText, success, question.Answer);
                }

                // Look for Next/Review/Submit button
                var (buttonType, button) = await FindNavigationButtonAsync();

                if (buttonType == "submit")
                {
                    // Final submission
                    progress?.Report("Submitting application...");
                    await HumanDelayAsync(1000, 2000);
                    await button!.ClickAsync();

                    session.LogAction("Submit", "Clicked submit button");

                    // Wait for confirmation
                    await Task.Delay(2500, cancellationToken);

                    // Check for success modal
                    var successIndicator = await _page.QuerySelectorAsync(
                        ".artdeco-modal:has-text('Application sent'), " +
                        ".artdeco-modal:has-text('application submitted'), " +
                        "[data-test-modal-id='post-apply-modal'], " +
                        ".jobs-post-apply-modal");

                    if (successIndicator != null)
                    {
                        session.Status = ApplicationSessionStatus.Submitted;
                        session.CompletedAt = DateTime.Now;
                        session.LogAction("Success", "Application submitted successfully");

                        // Try to dismiss success modal
                        await DismissModalAsync();

                        return true;
                    }

                    // Check for errors
                    var errorIndicator = await _page.QuerySelectorAsync(
                        ".artdeco-inline-feedback--error, .fb-form-element--error");

                    if (errorIndicator != null)
                    {
                        var errorText = await errorIndicator.InnerTextAsync();
                        session.Status = ApplicationSessionStatus.Failed;
                        session.ErrorMessage = $"Form validation error: {errorText}";
                        session.LogAction("Error", errorText, false);
                        return false;
                    }

                    // Unknown state - assume failure
                    session.Status = ApplicationSessionStatus.Failed;
                    session.ErrorMessage = "Submission confirmation not detected";
                    return false;
                }
                else if (buttonType == "next" || buttonType == "review")
                {
                    // Move to next page
                    progress?.Report($"Moving to page {currentPage + 2}...");
                    await HumanDelayAsync(500, 1000);
                    await button!.ClickAsync();
                    await HumanDelayAsync(800, 1500);

                    currentPage++;
                    questionsOnCurrentPage = session.Questions.Where(q => q.PageIndex == currentPage).ToList();
                    session.CurrentPage = currentPage;
                    session.LogAction("NextPage", $"Navigated to page {currentPage + 1}");
                }
                else
                {
                    // No button found - something went wrong
                    session.Status = ApplicationSessionStatus.Failed;
                    session.ErrorMessage = "Could not find navigation button";
                    return false;
                }
            }

            session.Status = ApplicationSessionStatus.Cancelled;
            return false;
        }
        catch (Exception ex)
        {
            session.Status = ApplicationSessionStatus.Failed;
            session.ErrorMessage = ex.Message;
            session.LogAction("Error", ex.Message, false);
            return false;
        }
    }

    public async Task CancelApplicationAsync()
    {
        if (_page == null) return;

        try
        {
            // Try to find and click dismiss/close button
            var dismissButton = await _page.QuerySelectorAsync(
                "button[aria-label='Dismiss'], " +
                "button[data-test-modal-close-btn], " +
                ".artdeco-modal__dismiss, " +
                ".mercado-match button:has-text('Discard')");

            if (dismissButton != null)
            {
                await dismissButton.ClickAsync();
                await Task.Delay(500);

                // Handle "Discard application?" confirmation dialog
                var discardConfirm = await _page.QuerySelectorAsync(
                    "button[data-test-dialog-primary-btn], " +
                    "button:has-text('Discard')");

                if (discardConfirm != null)
                {
                    await discardConfirm.ClickAsync();
                    await Task.Delay(500);
                }
            }
        }
        catch
        {
            // Ignore errors when cancelling
        }
    }

    #endregion

    #region Auto-Apply Helpers

    private async Task HumanDelayAsync(int? minMs = null, int? maxMs = null)
    {
        var settings = _settingsService.Settings;
        var min = minMs ?? settings.MinActionDelayMs;
        var max = maxMs ?? settings.MaxActionDelayMs;

        var delay = _random.Next(min, max);

        // Occasionally add extra "thinking" time (15% chance)
        if (_random.Next(100) < 15)
        {
            delay += _random.Next(500, 1500);
        }

        await Task.Delay(delay);
    }

    private async Task HumanTypeAsync(IElementHandle element, string text)
    {
        // Clear existing content first
        await element.FillAsync("");
        await Task.Delay(_random.Next(100, 300));

        // Type character by character with human-like delays
        foreach (var c in text)
        {
            await element.TypeAsync(c.ToString());
            await Task.Delay(_random.Next(30, 100)); // 30-100ms between keystrokes
        }
    }

    private async Task<IElementHandle?> FindEasyApplyButtonAsync()
    {
        var selectors = new[]
        {
            "button.jobs-apply-button--top-card",
            ".jobs-apply-button",
            "button:has-text('Easy Apply')",
            "[data-job-apply-button]",
            ".jobs-s-apply button"
        };

        foreach (var selector in selectors)
        {
            var button = await _page!.QuerySelectorAsync(selector);
            if (button != null)
            {
                var text = await button.InnerTextAsync();
                if (text.Contains("Easy Apply", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Apply", StringComparison.OrdinalIgnoreCase))
                {
                    return button;
                }
            }
        }

        return null;
    }

    private async Task<(List<EasyApplyQuestion> Questions, int TotalPages)> DetectAllQuestionsAsync(
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var allQuestions = new List<EasyApplyQuestion>();
        var pageIndex = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            progress?.Report($"Analyzing page {pageIndex + 1}...");

            // Detect questions on current page
            var pageQuestions = await DetectPageQuestionsAsync(pageIndex);
            allQuestions.AddRange(pageQuestions);

            // Look for Next button to see if there are more pages
            var (buttonType, button) = await FindNavigationButtonAsync();

            if (buttonType == "submit")
            {
                // We're on the final page
                break;
            }
            else if (buttonType == "next" || buttonType == "review")
            {
                // Navigate to next page to detect more questions
                await HumanDelayAsync(300, 600);
                await button!.ClickAsync();
                await HumanDelayAsync(600, 1000);
                pageIndex++;
            }
            else
            {
                // No button found - assume we're done
                break;
            }

            // Safety limit
            if (pageIndex > 10) break;
        }

        return (allQuestions, pageIndex + 1);
    }

    private async Task<List<EasyApplyQuestion>> DetectPageQuestionsAsync(int pageIndex)
    {
        var questions = new List<EasyApplyQuestion>();

        if (_page == null) return questions;

        // Find all form groups/fields in the modal
        var formGroups = await _page.QuerySelectorAllAsync(
            ".jobs-easy-apply-modal .fb-form-element, " +
            ".jobs-easy-apply-modal .jobs-easy-apply-form-section__grouping, " +
            ".jobs-easy-apply-content .fb-form-element");

        foreach (var group in formGroups)
        {
            try
            {
                var question = await ExtractQuestionFromElementAsync(group, pageIndex);
                if (question != null)
                {
                    questions.Add(question);
                }
            }
            catch
            {
                // Skip problematic form elements
            }
        }

        return questions;
    }

    private async Task<EasyApplyQuestion?> ExtractQuestionFromElementAsync(IElementHandle element, int pageIndex)
    {
        // Get label text
        var labelEl = await element.QuerySelectorAsync("label, .fb-form-element-label, .t-14");
        var labelText = labelEl != null ? await labelEl.InnerTextAsync() : "";
        labelText = labelText.Trim();

        if (string.IsNullOrEmpty(labelText) || labelText.Length < 3)
            return null;

        var question = new EasyApplyQuestion
        {
            QuestionText = labelText,
            PageIndex = pageIndex,
            Label = labelText
        };

        // Detect input type
        var textInput = await element.QuerySelectorAsync("input[type='text'], input[type='tel'], input[type='email'], input[type='number']");
        var textArea = await element.QuerySelectorAsync("textarea");
        var select = await element.QuerySelectorAsync("select");
        var radioButtons = await element.QuerySelectorAllAsync("input[type='radio']");
        var checkboxes = await element.QuerySelectorAllAsync("input[type='checkbox']");
        var fileInput = await element.QuerySelectorAsync("input[type='file']");

        if (textInput != null)
        {
            var inputType = await textInput.GetAttributeAsync("type") ?? "text";
            question.Type = inputType switch
            {
                "tel" => QuestionType.Phone,
                "email" => QuestionType.Email,
                "number" => QuestionType.Number,
                _ => QuestionType.Text
            };

            // Check if pre-filled
            var value = await textInput.GetAttributeAsync("value") ?? "";
            if (!string.IsNullOrEmpty(value))
            {
                question.IsPreFilled = true;
                question.PreFilledValue = value;
            }

            // Check for required
            var required = await textInput.GetAttributeAsync("required");
            var ariaRequired = await textInput.GetAttributeAsync("aria-required");
            question.IsRequired = required != null || ariaRequired == "true";

            // Build selector for this input
            var inputId = await textInput.GetAttributeAsync("id");
            question.Selector = !string.IsNullOrEmpty(inputId) ? $"#{inputId}" : "input[type='text']";
        }
        else if (textArea != null)
        {
            question.Type = QuestionType.TextArea;
            var value = await textArea.InnerTextAsync();
            if (!string.IsNullOrEmpty(value?.Trim()))
            {
                question.IsPreFilled = true;
                question.PreFilledValue = value;
            }

            var textAreaId = await textArea.GetAttributeAsync("id");
            question.Selector = !string.IsNullOrEmpty(textAreaId) ? $"#{textAreaId}" : "textarea";
        }
        else if (select != null)
        {
            question.Type = QuestionType.Select;

            // Get options
            var options = await select.QuerySelectorAllAsync("option");
            foreach (var option in options)
            {
                var optionText = await option.InnerTextAsync();
                if (!string.IsNullOrWhiteSpace(optionText) && optionText != "Select an option")
                {
                    question.Options.Add(optionText.Trim());
                }
            }

            var selectId = await select.GetAttributeAsync("id");
            question.Selector = !string.IsNullOrEmpty(selectId) ? $"#{selectId}" : "select";
        }
        else if (radioButtons.Count > 0)
        {
            question.Type = QuestionType.Radio;

            // Detect Yes/No pattern
            var optionTexts = new List<string>();
            foreach (var radio in radioButtons)
            {
                var radioLabel = await radio.EvaluateAsync<string>("el => el.parentElement?.textContent || el.nextSibling?.textContent || ''");
                if (!string.IsNullOrWhiteSpace(radioLabel))
                {
                    optionTexts.Add(radioLabel.Trim());
                }
            }
            question.Options = optionTexts;

            if (optionTexts.Any(o => o.Contains("Yes", StringComparison.OrdinalIgnoreCase)) &&
                optionTexts.Any(o => o.Contains("No", StringComparison.OrdinalIgnoreCase)))
            {
                question.Type = QuestionType.YesNo;
            }

            var firstName = await radioButtons.First().GetAttributeAsync("name");
            question.Selector = !string.IsNullOrEmpty(firstName) ? $"input[name='{firstName}']" : "input[type='radio']";
        }
        else if (checkboxes.Count > 0)
        {
            question.Type = QuestionType.Checkbox;

            foreach (var checkbox in checkboxes)
            {
                var checkLabel = await checkbox.EvaluateAsync<string>("el => el.parentElement?.textContent || ''");
                if (!string.IsNullOrWhiteSpace(checkLabel))
                {
                    question.Options.Add(checkLabel.Trim());
                }
            }
        }
        else if (fileInput != null)
        {
            question.Type = QuestionType.FileUpload;

            // Check if resume is already uploaded
            var uploadedFile = await element.QuerySelectorAsync(".jobs-document-upload__file-name, .artdeco-button--tertiary");
            if (uploadedFile != null)
            {
                question.IsPreFilled = true;
                question.PreFilledValue = await uploadedFile.InnerTextAsync();
            }
        }
        else
        {
            question.Type = QuestionType.Unknown;
        }

        return question;
    }

    private async Task<bool> FillQuestionAsync(EasyApplyQuestion question)
    {
        if (_page == null || string.IsNullOrEmpty(question.Answer))
            return false;

        try
        {
            switch (question.Type)
            {
                case QuestionType.Text:
                case QuestionType.Phone:
                case QuestionType.Email:
                case QuestionType.Number:
                    var textInput = await FindInputByLabelAsync(question.QuestionText, "input");
                    if (textInput != null)
                    {
                        await textInput.FillAsync("");
                        await HumanDelayAsync(100, 300);
                        await HumanTypeAsync(textInput, question.Answer);
                        return true;
                    }
                    break;

                case QuestionType.TextArea:
                    var textArea = await FindInputByLabelAsync(question.QuestionText, "textarea");
                    if (textArea != null)
                    {
                        await textArea.FillAsync("");
                        await HumanDelayAsync(100, 300);
                        await HumanTypeAsync(textArea, question.Answer);
                        return true;
                    }
                    break;

                case QuestionType.Select:
                    var select = await FindInputByLabelAsync(question.QuestionText, "select");
                    if (select != null)
                    {
                        await select.SelectOptionAsync(new SelectOptionValue { Label = question.Answer });
                        return true;
                    }
                    break;

                case QuestionType.Radio:
                case QuestionType.YesNo:
                    // Find radio button with matching value/label
                    var radioSelector = $"input[type='radio']";
                    var radios = await _page.QuerySelectorAllAsync(radioSelector);
                    foreach (var radio in radios)
                    {
                        var radioText = await radio.EvaluateAsync<string>("el => el.parentElement?.textContent || el.labels?.[0]?.textContent || ''");
                        if (radioText.Contains(question.Answer, StringComparison.OrdinalIgnoreCase))
                        {
                            await radio.ClickAsync();
                            return true;
                        }
                    }
                    break;

                case QuestionType.Checkbox:
                    // For single checkbox, check it if answer indicates yes
                    var checkbox = await FindInputByLabelAsync(question.QuestionText, "input[type='checkbox']");
                    if (checkbox != null)
                    {
                        var shouldCheck = question.Answer.Contains("yes", StringComparison.OrdinalIgnoreCase) ||
                                         question.Answer.Contains("true", StringComparison.OrdinalIgnoreCase);
                        var isChecked = await checkbox.IsCheckedAsync();

                        if (shouldCheck && !isChecked)
                        {
                            await checkbox.ClickAsync();
                        }
                        return true;
                    }
                    break;

                case QuestionType.FileUpload:
                    // Skip file uploads - user should have resume pre-uploaded
                    return true;

                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private async Task<IElementHandle?> FindInputByLabelAsync(string labelText, string inputSelector)
    {
        if (_page == null) return null;

        // Strategy 1: Find by label's for attribute
        var labels = await _page.QuerySelectorAllAsync("label");
        foreach (var label in labels)
        {
            var text = await label.InnerTextAsync();
            if (text.Contains(labelText, StringComparison.OrdinalIgnoreCase))
            {
                var forAttr = await label.GetAttributeAsync("for");
                if (!string.IsNullOrEmpty(forAttr))
                {
                    var input = await _page.QuerySelectorAsync($"#{forAttr}");
                    if (input != null) return input;
                }

                // Strategy 2: Input is inside or next to label
                var inputInLabel = await label.QuerySelectorAsync(inputSelector);
                if (inputInLabel != null) return inputInLabel;
            }
        }

        // Strategy 3: Find by aria-label
        var inputWithAria = await _page.QuerySelectorAsync($"{inputSelector}[aria-label*='{labelText}']");
        if (inputWithAria != null) return inputWithAria;

        // Strategy 4: Find by placeholder
        var inputWithPlaceholder = await _page.QuerySelectorAsync($"{inputSelector}[placeholder*='{labelText}']");
        if (inputWithPlaceholder != null) return inputWithPlaceholder;

        return null;
    }

    private async Task<(string Type, IElementHandle? Button)> FindNavigationButtonAsync()
    {
        if (_page == null) return ("none", null);

        // Check for Submit button first
        var submitSelectors = new[]
        {
            "button[aria-label='Submit application']",
            "button:has-text('Submit application')",
            "button[data-easy-apply-next-button]:has-text('Submit')"
        };

        foreach (var selector in submitSelectors)
        {
            var btn = await _page.QuerySelectorAsync(selector);
            if (btn != null && await btn.IsVisibleAsync())
            {
                return ("submit", btn);
            }
        }

        // Check for Review button
        var reviewSelectors = new[]
        {
            "button:has-text('Review')",
            "button[aria-label='Review your application']"
        };

        foreach (var selector in reviewSelectors)
        {
            var btn = await _page.QuerySelectorAsync(selector);
            if (btn != null && await btn.IsVisibleAsync())
            {
                return ("review", btn);
            }
        }

        // Check for Next button
        var nextSelectors = new[]
        {
            "button[aria-label='Continue to next step']",
            "button:has-text('Next')",
            "button[data-easy-apply-next-button]"
        };

        foreach (var selector in nextSelectors)
        {
            var btn = await _page.QuerySelectorAsync(selector);
            if (btn != null && await btn.IsVisibleAsync())
            {
                return ("next", btn);
            }
        }

        return ("none", null);
    }

    private async Task NavigateToFirstPageAsync()
    {
        if (_page == null) return;

        // Try to find and click Back button until we're at the first page
        for (int i = 0; i < 10; i++)
        {
            var backButton = await _page.QuerySelectorAsync(
                "button[aria-label='Back'], " +
                "button:has-text('Back')");

            if (backButton == null || !await backButton.IsVisibleAsync())
            {
                break;
            }

            await backButton.ClickAsync();
            await Task.Delay(500);
        }
    }

    private async Task DismissModalAsync()
    {
        if (_page == null) return;

        try
        {
            // Try multiple dismiss patterns
            var dismissButton = await _page.QuerySelectorAsync(
                "button[aria-label='Dismiss'], " +
                ".artdeco-modal__dismiss, " +
                ".artdeco-button--circle");

            if (dismissButton != null)
            {
                await dismissButton.ClickAsync();
            }
        }
        catch
        {
            // Ignore dismiss errors
        }
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..(maxLength - 3)] + "...";
    }

    #endregion

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
