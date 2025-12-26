using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobFinder.Models;
using JobFinder.Services;

namespace JobFinder.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IJobRepository _jobRepository;
    private readonly ILinkedInService _linkedInService;
    private readonly IKimiService _kimiService;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private ObservableCollection<JobViewModel> _jobs = [];

    [ObservableProperty]
    private JobViewModel? _selectedJob;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string _searchProgress = "";

    // Filter properties
    [ObservableProperty]
    private string _jobTitle = ".NET Developer";

    [ObservableProperty]
    private bool _filterMidLevel = true;

    [ObservableProperty]
    private bool _filterSeniorLevel = true;

    [ObservableProperty]
    private string _location = "Croatia";

    [ObservableProperty]
    private bool _remoteOnly = true;

    [ObservableProperty]
    private int _maxResults = 50;

    // Filter for job status view
    [ObservableProperty]
    private ApplicationStatus? _statusFilter;

    public MainViewModel(IJobRepository jobRepository, ILinkedInService linkedInService, IKimiService kimiService, ISettingsService settingsService)
    {
        _jobRepository = jobRepository;
        _linkedInService = linkedInService;
        _kimiService = kimiService;
        _settingsService = settingsService;
    }

    public async Task InitializeAsync()
    {
        await _jobRepository.InitializeDatabaseAsync();
        await LoadJobsAsync();
        StatusMessage = $"Loaded {Jobs.Count} jobs from database. Starting LinkedIn...";

        // Auto-start LinkedIn and search
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsSearching) return;

        try
        {
            // Open LinkedIn browser if not already open
            if (!_linkedInService.IsBrowserOpen)
            {
                StatusMessage = "Opening LinkedIn...";
                await _linkedInService.OpenLoginWindowAsync();
                await Task.Delay(3000);
            }

            // Check login status
            StatusMessage = "Checking login status...";
            IsLoggedIn = await _linkedInService.CheckLoginStatusAsync();

            if (!IsLoggedIn)
            {
                StatusMessage = "Please log in to LinkedIn in the browser window, then click Refresh again.";
                return;
            }

            // Search for jobs
            StatusMessage = "Logged in! Searching for jobs...";
            await SearchJobsAsync();

            // After search, fetch missing details for ALL existing jobs
            IsSearching = true;
            _searchCts = new CancellationTokenSource();
            var allJobs = await _jobRepository.GetAllJobsAsync();
            await FetchMissingDetailsAsync(allJobs, _searchCts.Token);
            await LoadJobsAsync();
            IsSearching = false;
            SearchProgress = "";
            StatusMessage = "Done.";
        }
        catch (Exception ex)
        {
            IsSearching = false;
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadJobsAsync()
    {
        var includeDiscarded = _settingsService.Settings.ShowDiscardedJobs;
        var jobs = StatusFilter.HasValue
            ? await _jobRepository.GetJobsByStatusAsync(StatusFilter.Value, includeDiscarded)
            : await _jobRepository.GetAllJobsAsync(includeDiscarded);

        Jobs.Clear();
        foreach (var job in jobs)
        {
            Jobs.Add(new JobViewModel(job));
        }
    }

    private async Task SearchJobsAsync()
    {
        if (!IsLoggedIn)
        {
            StatusMessage = "Please log in to LinkedIn first.";
            return;
        }

        IsSearching = true;
        _searchCts = new CancellationTokenSource();

        try
        {
            var filter = new SearchFilter
            {
                JobTitle = JobTitle,
                ExperienceLevels = GetSelectedExperienceLevels(),
                Locations = [Location],
                RemoteOnly = RemoteOnly,
                MaxResults = MaxResults
            };

            var progress = new Progress<string>(msg =>
            {
                SearchProgress = msg;
                StatusMessage = msg;
            });

            var foundJobs = await _linkedInService.SearchJobsAsync(filter, progress, _searchCts.Token);

            // Save to database (only adds new jobs)
            var newJobsCount = await _jobRepository.AddJobsAsync(foundJobs);

            // Fetch details for new jobs
            if (newJobsCount > 0)
            {
                await FetchMissingDetailsAsync(foundJobs, _searchCts.Token);
            }

            // Refresh the list
            await LoadJobsAsync();

            StatusMessage = $"Done. Found {foundJobs.Count} jobs, {newJobsCount} new.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Search cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Search error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
            SearchProgress = "";
        }
    }

    [RelayCommand]
    private void CancelSearch()
    {
        _searchCts?.Cancel();
        StatusMessage = "Cancelling search...";
    }

    [RelayCommand]
    private async Task ApplyToJobAsync()
    {
        if (SelectedJob == null)
        {
            StatusMessage = "Please select a job first.";
            return;
        }

        try
        {
            if (SelectedJob.HasEasyApply)
            {
                StatusMessage = "Opening Easy Apply...";
                var success = await _linkedInService.StartEasyApplyAsync(SelectedJob.JobUrl ?? "");
                StatusMessage = success
                    ? "Easy Apply opened. Complete the application in the browser."
                    : "Could not open Easy Apply.";
            }
            else if (!string.IsNullOrEmpty(SelectedJob.ExternalApplyUrl))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = SelectedJob.ExternalApplyUrl,
                    UseShellExecute = true
                });
                StatusMessage = "Opening external application link...";
            }
            else if (!string.IsNullOrEmpty(SelectedJob.JobUrl))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = SelectedJob.JobUrl,
                    UseShellExecute = true
                });
                StatusMessage = "Opening job page in browser...";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task MarkAsAppliedAsync()
    {
        if (SelectedJob == null) return;

        await _jobRepository.UpdateJobStatusAsync(SelectedJob.Id, ApplicationStatus.Applied);
        SelectedJob.Status = ApplicationStatus.Applied;
        SelectedJob.DateApplied = DateTime.Now;
        StatusMessage = $"Marked '{SelectedJob.Title}' as Applied.";
    }

    [RelayCommand]
    private async Task MarkAsSavedAsync()
    {
        if (SelectedJob == null) return;

        await _jobRepository.UpdateJobStatusAsync(SelectedJob.Id, ApplicationStatus.Saved);
        SelectedJob.Status = ApplicationStatus.Saved;
        StatusMessage = $"Saved '{SelectedJob.Title}'.";
    }

    [RelayCommand]
    private async Task MarkAsIgnoredAsync()
    {
        if (SelectedJob == null) return;

        await _jobRepository.UpdateJobStatusAsync(SelectedJob.Id, ApplicationStatus.Ignored);
        SelectedJob.Status = ApplicationStatus.Ignored;
        StatusMessage = $"Ignored '{SelectedJob.Title}'.";
    }

    [RelayCommand]
    private async Task DiscardJobAsync()
    {
        if (SelectedJob == null) return;

        var jobTitle = SelectedJob.Title;
        await _jobRepository.DiscardJobAsync(SelectedJob.Id);

        // If not showing discarded jobs, remove from list
        if (!_settingsService.Settings.ShowDiscardedJobs)
        {
            Jobs.Remove(SelectedJob);
            SelectedJob = null;
        }
        else
        {
            SelectedJob.IsDiscarded = true;
        }
        StatusMessage = $"Discarded '{jobTitle}'.";
    }

    [RelayCommand]
    private async Task RestoreJobAsync()
    {
        if (SelectedJob == null) return;

        await _jobRepository.RestoreJobAsync(SelectedJob.Id);
        SelectedJob.IsDiscarded = false;
        StatusMessage = $"Restored '{SelectedJob.Title}'.";
    }

    [RelayCommand]
    private async Task RestoreAllJobsAsync()
    {
        var count = await _jobRepository.RestoreAllJobsAsync();
        await LoadJobsAsync();
        StatusMessage = $"Restored {count} discarded jobs.";
    }

    [RelayCommand]
    private async Task GetJobDetailsAsync()
    {
        if (SelectedJob == null || string.IsNullOrEmpty(SelectedJob.JobUrl))
        {
            StatusMessage = "Please select a job first.";
            return;
        }

        try
        {
            StatusMessage = "Fetching job details...";
            var details = await _linkedInService.GetJobDetailsAsync(SelectedJob.JobUrl);

            if (details != null)
            {
                SelectedJob.Description = details.Description;
                SelectedJob.HasEasyApply = details.HasEasyApply;
                SelectedJob.ExternalApplyUrl = details.ExternalApplyUrl;
                SelectedJob.RecruiterEmail = details.RecruiterEmail;

                // Update in database
                var job = await _jobRepository.GetJobByLinkedInIdAsync(SelectedJob.LinkedInJobId);
                if (job != null)
                {
                    job.Description = details.Description;
                    job.HasEasyApply = details.HasEasyApply;
                    job.ExternalApplyUrl = details.ExternalApplyUrl;
                    job.RecruiterEmail = details.RecruiterEmail;
                    await _jobRepository.UpdateJobAsync(job);
                }

                StatusMessage = "Job details loaded.";
            }
            else
            {
                StatusMessage = "Could not fetch job details.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenJobInBrowser()
    {
        if (SelectedJob?.JobUrl == null) return;

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = SelectedJob.JobUrl,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void CopyRecruiterEmail()
    {
        if (string.IsNullOrEmpty(SelectedJob?.RecruiterEmail)) return;

        Clipboard.SetText(SelectedJob.RecruiterEmail);
        StatusMessage = "Email copied to clipboard.";
    }

    [RelayCommand]
    private async Task FilterByStatusAsync(ApplicationStatus? status)
    {
        StatusFilter = status;
        await LoadJobsAsync();
        StatusMessage = status.HasValue
            ? $"Showing {status.Value} jobs"
            : "Showing all jobs";
    }

    [RelayCommand]
    private async Task CloseBrowserAsync()
    {
        await _linkedInService.CloseAsync();
        IsLoggedIn = false;
        StatusMessage = "Browser closed.";
    }

    [RelayCommand]
    private async Task ReanalyzeAllJobsAsync()
    {
        if (!_kimiService.IsConfigured)
        {
            StatusMessage = "Kimi API key not configured. Go to Settings to add it.";
            return;
        }

        if (IsSearching)
        {
            StatusMessage = "Please wait for current operation to complete.";
            return;
        }

        IsSearching = true;
        _searchCts = new CancellationTokenSource();

        try
        {
            // Get all non-discarded jobs with descriptions
            var allJobs = await _jobRepository.GetAllJobsAsync(includeDiscarded: false);
            var jobsToAnalyze = allJobs.Where(j => !string.IsNullOrEmpty(j.Description)).ToList();

            if (jobsToAnalyze.Count == 0)
            {
                StatusMessage = "No jobs with descriptions to analyze.";
                return;
            }

            SearchProgress = $"Re-analyzing {jobsToAnalyze.Count} jobs...";
            StatusMessage = SearchProgress;
            int analyzed = 0;

            foreach (var job in jobsToAnalyze)
            {
                if (_searchCts.Token.IsCancellationRequested) break;

                try
                {
                    var result = await _kimiService.GetSummaryAsync(job.Description!, _searchCts.Token);
                    if (result != null && !string.IsNullOrEmpty(result.Summary))
                    {
                        job.SummaryCroatian = result.Summary;
                        job.ShortSummary = result.ShortSummary;
                        job.Rating = result.Rating;
                        job.DiscardReason = result.DiscardReason;

                        // Auto-discard disabled - just save the analysis results
                        // User can manually discard later based on rating/recommendation
                        // Store the AI's recommendation for reference
                        if (result.ShouldDiscard && string.IsNullOrEmpty(job.DiscardReason))
                        {
                            job.DiscardReason = result.DiscardReason ?? "AI recommended discard";
                        }

                        await _jobRepository.UpdateJobAsync(job);
                        analyzed++;

                        var parseWarning = result.ParseFailed ? " ⚠️PARSE FAILED" : "";
                        var discardRecommendation = result.ShouldDiscard ? " [AI: discard]" : "";
                        SearchProgress = $"Re-analyzed {analyzed}/{jobsToAnalyze.Count}: {job.Title} (Rating: {result.Rating}/10){discardRecommendation}{parseWarning}";
                        StatusMessage = SearchProgress;
                    }
                }
                catch
                {
                    // Skip failed analyses
                }

                // Delay between API calls
                await Task.Delay(1000, _searchCts.Token);
            }

            await LoadJobsAsync();
            StatusMessage = $"Re-analysis complete. Processed {analyzed} jobs.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Re-analysis cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Re-analysis error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
            SearchProgress = "";
        }
    }

    private List<string> GetSelectedExperienceLevels()
    {
        var levels = new List<string>();
        if (FilterMidLevel) levels.Add("Mid-Senior level");
        if (FilterSeniorLevel) levels.Add("Senior");
        return levels;
    }

    private async Task FetchMissingDetailsAsync(IEnumerable<Job> jobs, CancellationToken cancellationToken)
    {
        var jobsList = jobs.ToList();
        var jobsWithoutDetails = jobsList.Where(j => !j.IsDiscarded && string.IsNullOrEmpty(j.Description) && !string.IsNullOrEmpty(j.JobUrl)).ToList();

        if (jobsWithoutDetails.Count > 0)
        {
            SearchProgress = $"Fetching details for {jobsWithoutDetails.Count} jobs...";
            StatusMessage = SearchProgress;
            int detailsFetched = 0;

            foreach (var job in jobsWithoutDetails)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var details = await _linkedInService.GetJobDetailsAsync(job.JobUrl!);
                    if (details != null)
                    {
                        job.Description = details.Description;
                        job.HasEasyApply = details.HasEasyApply;
                        job.ExternalApplyUrl = details.ExternalApplyUrl;
                        job.RecruiterEmail = details.RecruiterEmail;

                        // Update in database
                        var dbJob = await _jobRepository.GetJobByLinkedInIdAsync(job.LinkedInJobId);
                        if (dbJob != null)
                        {
                            dbJob.Description = details.Description;
                            dbJob.HasEasyApply = details.HasEasyApply;
                            dbJob.ExternalApplyUrl = details.ExternalApplyUrl;
                            dbJob.RecruiterEmail = details.RecruiterEmail;
                            await _jobRepository.UpdateJobAsync(dbJob);
                        }

                        detailsFetched++;
                        SearchProgress = $"Fetched details {detailsFetched}/{jobsWithoutDetails.Count}: {job.Title}";
                        StatusMessage = SearchProgress;
                    }
                }
                catch
                {
                    // Skip failed fetches
                }

                // Small delay to avoid rate limiting
                await Task.Delay(500, cancellationToken);
            }
        }

        // Always try to fetch AI summaries for jobs missing Croatian summary
        await FetchMissingSummariesAsync(jobsList, cancellationToken);
    }

    private async Task FetchMissingSummariesAsync(IEnumerable<Job> jobs, CancellationToken cancellationToken)
    {
        if (!_kimiService.IsConfigured)
        {
            StatusMessage = "Kimi API key not configured. Go to Settings to add it.";
            return;
        }

        var jobsWithoutSummary = jobs.Where(j =>
            !j.IsDiscarded &&
            !string.IsNullOrEmpty(j.Description) &&
            string.IsNullOrEmpty(j.SummaryCroatian)).ToList();

        if (jobsWithoutSummary.Count == 0) return;

        SearchProgress = $"Getting AI summaries for {jobsWithoutSummary.Count} jobs...";
        StatusMessage = SearchProgress;
        int summariesFetched = 0;

        foreach (var job in jobsWithoutSummary)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var result = await _kimiService.GetSummaryAsync(job.Description!, cancellationToken);
                if (result != null && !string.IsNullOrEmpty(result.Summary))
                {
                    job.SummaryCroatian = result.Summary;
                    job.ShortSummary = result.ShortSummary;
                    job.Rating = result.Rating;
                    job.DiscardReason = result.DiscardReason;

                    // Auto-discard disabled - just save the analysis results
                    // Store the AI's recommendation for reference
                    if (result.ShouldDiscard && string.IsNullOrEmpty(job.DiscardReason))
                    {
                        job.DiscardReason = result.DiscardReason ?? "AI recommended discard";
                    }

                    // Update in database
                    var dbJob = await _jobRepository.GetJobByLinkedInIdAsync(job.LinkedInJobId);
                    if (dbJob != null)
                    {
                        dbJob.SummaryCroatian = result.Summary;
                        dbJob.ShortSummary = result.ShortSummary;
                        dbJob.Rating = result.Rating;
                        dbJob.DiscardReason = job.DiscardReason;
                        await _jobRepository.UpdateJobAsync(dbJob);
                    }

                    summariesFetched++;
                    var parseWarning = result.ParseFailed ? " ⚠️PARSE FAILED" : "";
                    var discardRecommendation = result.ShouldDiscard ? " [AI: discard]" : "";
                    SearchProgress = $"AI summary {summariesFetched}/{jobsWithoutSummary.Count}: {job.Title} (Rating: {result.Rating}/10){discardRecommendation}{parseWarning}";
                    StatusMessage = SearchProgress;
                }
            }
            catch
            {
                // Skip failed summaries
            }

            // Delay between API calls
            await Task.Delay(1000, cancellationToken);
        }
    }
}
