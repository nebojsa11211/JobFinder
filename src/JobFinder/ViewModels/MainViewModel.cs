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
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private ObservableCollection<JobViewModel> _jobs = [];

    [ObservableProperty]
    private JobViewModel? _selectedJob;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchJobsCommand))]
    private bool _isSearching;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchJobsCommand))]
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

    public MainViewModel(IJobRepository jobRepository, ILinkedInService linkedInService)
    {
        _jobRepository = jobRepository;
        _linkedInService = linkedInService;
    }

    public async Task InitializeAsync()
    {
        await _jobRepository.InitializeDatabaseAsync();
        await LoadJobsAsync();
        StatusMessage = $"Loaded {Jobs.Count} jobs from database";
    }

    [RelayCommand]
    private async Task LoadJobsAsync()
    {
        var jobs = StatusFilter.HasValue
            ? await _jobRepository.GetJobsByStatusAsync(StatusFilter.Value)
            : await _jobRepository.GetAllJobsAsync();

        Jobs.Clear();
        foreach (var job in jobs)
        {
            Jobs.Add(new JobViewModel(job));
        }
    }

    [RelayCommand]
    private async Task OpenLoginAsync()
    {
        try
        {
            StatusMessage = "Opening LinkedIn login...";
            await _linkedInService.OpenLoginWindowAsync();
            StatusMessage = "Please log in to LinkedIn. Click 'Check Login' when done.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening browser: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CheckLoginAsync()
    {
        try
        {
            StatusMessage = "Checking login status...";
            IsLoggedIn = await _linkedInService.CheckLoginStatusAsync();
            StatusMessage = IsLoggedIn
                ? "Successfully logged in to LinkedIn!"
                : "Not logged in. Please complete the login process.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error checking login: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
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

            // Save to database
            var jobEntities = foundJobs.Select(j => j).ToList();
            await _jobRepository.AddJobsAsync(jobEntities);

            // Refresh the list
            await LoadJobsAsync();

            StatusMessage = $"Search complete. Found {foundJobs.Count} jobs.";
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

    private bool CanSearch() => IsLoggedIn && !IsSearching;

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

    private List<string> GetSelectedExperienceLevels()
    {
        var levels = new List<string>();
        if (FilterMidLevel) levels.Add("Mid-Senior level");
        if (FilterSeniorLevel) levels.Add("Senior");
        return levels;
    }
}
