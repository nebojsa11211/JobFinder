using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobFinder.Models;
using JobFinder.Services;

namespace JobFinder.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IJobRepository _jobRepository;
    private readonly IJobPlatformServiceFactory _platformServiceFactory;
    private readonly ILinkedInService _linkedInService;
    private readonly IKimiService _kimiService;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _autoApplyCts;

    [ObservableProperty]
    private ObservableCollection<JobViewModel> _jobs = [];

    [ObservableProperty]
    private JobViewModel? _selectedJob;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isAutoApplying;

    [ObservableProperty]
    private string _autoApplyProgress = "";

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string _searchProgress = "";

    // Platform selection
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLinkedInSelected))]
    [NotifyPropertyChangedFor(nameof(IsUpworkSelected))]
    [NotifyPropertyChangedFor(nameof(PlatformDisplayName))]
    private JobPlatform _selectedPlatform = JobPlatform.LinkedIn;

    public List<JobPlatform> AvailablePlatforms { get; } = [JobPlatform.LinkedIn, JobPlatform.Upwork];
    public bool IsLinkedInSelected => SelectedPlatform == JobPlatform.LinkedIn;
    public bool IsUpworkSelected => SelectedPlatform == JobPlatform.Upwork;
    public string PlatformDisplayName => SelectedPlatform.ToString();

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

    // Platform filter for job list (null = show all platforms)
    [ObservableProperty]
    private JobPlatform? _platformFilter;

    /// <summary>
    /// Gets the currently selected platform service.
    /// </summary>
    private IJobPlatformService CurrentPlatformService => _platformServiceFactory.GetService(SelectedPlatform);

    public MainViewModel(
        IJobRepository jobRepository,
        IJobPlatformServiceFactory platformServiceFactory,
        ILinkedInService linkedInService,
        IKimiService kimiService,
        ISettingsService settingsService)
    {
        _jobRepository = jobRepository;
        _platformServiceFactory = platformServiceFactory;
        _linkedInService = linkedInService;
        _kimiService = kimiService;
        _settingsService = settingsService;
    }

    public async Task InitializeAsync()
    {
        await _jobRepository.InitializeDatabaseAsync();
        await LoadJobsAsync();
        StatusMessage = $"Loaded {Jobs.Count} jobs from database. Select a platform and click Refresh to search.";
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsSearching) return;

        var platformService = CurrentPlatformService;
        var platformName = SelectedPlatform.ToString();

        try
        {
            // Open browser if not already open
            if (!platformService.IsBrowserOpen)
            {
                StatusMessage = $"Opening {platformName}...";
                await platformService.OpenLoginWindowAsync();
                await Task.Delay(3000);
            }

            // Check login status
            StatusMessage = "Checking login status...";
            IsLoggedIn = await platformService.CheckLoginStatusAsync();

            if (!IsLoggedIn)
            {
                StatusMessage = $"Please log in to {platformName} in the browser window, then click Refresh again.";
                return;
            }

            // Search for jobs
            StatusMessage = $"Logged in! Searching {platformName} for jobs...";
            await SearchJobsAsync();

            // After search, fetch missing details for jobs from this platform
            IsSearching = true;
            _searchCts = new CancellationTokenSource();
            var platformJobs = await _jobRepository.GetJobsByPlatformAsync(SelectedPlatform);
            await FetchMissingDetailsAsync(platformJobs, _searchCts.Token);
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
        var platformService = CurrentPlatformService;

        if (!IsLoggedIn)
        {
            StatusMessage = $"Please log in to {SelectedPlatform} first.";
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

            var foundJobs = await platformService.SearchJobsAsync(filter, progress, _searchCts.Token);

            // Save to database (only adds new jobs)
            var newJobsCount = await _jobRepository.AddJobsAsync(foundJobs);

            // Fetch details for new jobs
            if (newJobsCount > 0)
            {
                await FetchMissingDetailsAsync(foundJobs, _searchCts.Token);
            }

            // Refresh the list
            await LoadJobsAsync();

            StatusMessage = $"Done. Found {foundJobs.Count} jobs on {SelectedPlatform}, {newJobsCount} new.";
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
                // Platform-specific quick apply
                if (SelectedJob.Platform == JobPlatform.LinkedIn)
                {
                    StatusMessage = "Opening Easy Apply...";
                    var success = await _linkedInService.StartEasyApplyAsync(SelectedJob.JobUrl ?? "");
                    StatusMessage = success
                        ? "Easy Apply opened. Complete the application in the browser."
                        : "Could not open Easy Apply.";
                }
                else if (SelectedJob.Platform == JobPlatform.Upwork)
                {
                    // For Upwork, open the job URL - proposals are submitted through AutoApply
                    StatusMessage = "Opening Upwork job page...";
                    if (!string.IsNullOrEmpty(SelectedJob.JobUrl))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = SelectedJob.JobUrl,
                            UseShellExecute = true
                        });
                        StatusMessage = "Use Auto Apply to submit a proposal, or apply manually in the browser.";
                    }
                }
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

    /// <summary>
    /// Initiates the AI-powered automated Easy Apply flow with human-in-the-loop approval.
    /// </summary>
    [RelayCommand]
    private async Task AutoApplyAsync()
    {
        if (SelectedJob == null)
        {
            StatusMessage = "Please select a job first.";
            return;
        }

        if (!SelectedJob.HasEasyApply)
        {
            StatusMessage = "This job does not support Easy Apply.";
            return;
        }

        if (!IsLoggedIn)
        {
            StatusMessage = "Please log in to LinkedIn first.";
            return;
        }

        var userProfile = _settingsService.Settings.UserProfessionalProfile;
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            StatusMessage = "Please configure your Professional Profile in Settings first.";
            return;
        }

        if (!_kimiService.IsConfigured)
        {
            StatusMessage = "Please configure Kimi API key in Settings first.";
            return;
        }

        if (IsAutoApplying || IsSearching)
        {
            StatusMessage = "Another operation is in progress.";
            return;
        }

        IsAutoApplying = true;
        _autoApplyCts = new CancellationTokenSource();
        ApplicationSession? session = null;

        try
        {
            var jobUrl = SelectedJob.JobUrl ?? "";
            var jobTitle = SelectedJob.Title;
            var company = SelectedJob.CompanyName;
            var description = SelectedJob.Description ?? "";

            // Step 1: Get the job from database for full details
            var job = await _jobRepository.GetJobByExternalIdAsync(SelectedJob.Platform, SelectedJob.ExternalJobId);
            if (job == null)
            {
                StatusMessage = "Could not find job in database.";
                return;
            }

            AutoApplyProgress = "Preparing application...";
            StatusMessage = AutoApplyProgress;

            // Step 2: Prepare the application (opens modal, detects questions)
            var progress = new Progress<string>(msg =>
            {
                AutoApplyProgress = msg;
                StatusMessage = msg;
            });

            session = await _linkedInService.PrepareApplicationAsync(job, progress, _autoApplyCts.Token);

            if (session == null)
            {
                StatusMessage = "Could not prepare application. Modal may not have opened.";
                await _linkedInService.CancelApplicationAsync();
                return;
            }

            session.LogAction("Prepare", "Application modal opened and questions detected", true);

            // Step 3: Generate AI message
            AutoApplyProgress = "Generating personalized message...";
            StatusMessage = AutoApplyProgress;

            var messageResult = await _kimiService.GenerateApplicationMessageAsync(
                description, jobTitle, company, userProfile, _autoApplyCts.Token);

            if (messageResult != null)
            {
                session.ApplicationMessage = messageResult.Message;
                session.MatchingSkills = messageResult.MatchingSkills;
                session.AddressedRequirements = messageResult.AddressedRequirements;
                session.ConfidenceScore = messageResult.ConfidenceScore;
                session.LogAction("AI", "Generated application message", true,
                    $"Confidence: {messageResult.ConfidenceScore}%");
            }
            else
            {
                session.ApplicationMessage = "";
                session.ConfidenceScore = 0;
                session.LogAction("AI", "Failed to generate message", false);
            }

            // Step 4: Generate answers for questions
            var unansweredQuestions = session.Questions.Where(q => !q.IsPreFilled).ToList();
            if (unansweredQuestions.Count > 0)
            {
                AutoApplyProgress = $"Generating answers for {unansweredQuestions.Count} questions...";
                StatusMessage = AutoApplyProgress;

                var answersResult = await _kimiService.GenerateQuestionAnswersAsync(
                    unansweredQuestions, userProfile, description, _autoApplyCts.Token);

                if (answersResult.Success)
                {
                    foreach (var question in unansweredQuestions)
                    {
                        if (answersResult.Answers.TryGetValue(question.QuestionText, out var answer))
                        {
                            question.Answer = answer;
                        }
                    }
                    session.LogAction("AI", $"Generated answers for {answersResult.Answers.Count} questions", true);
                }
                else
                {
                    session.LogAction("AI", "Failed to generate question answers", false, answersResult.ErrorMessage);
                }
            }

            // Step 5: Show review dialog - CRITICAL HUMAN-IN-THE-LOOP STEP
            session.Status = ApplicationSessionStatus.ReadyForReview;
            AutoApplyProgress = "Waiting for your approval...";
            StatusMessage = "Review and approve the application before submitting.";

            var reviewVm = new ApplicationReviewViewModel(session);
            var reviewWindow = new ApplicationReviewWindow(reviewVm)
            {
                Owner = Application.Current.MainWindow
            };

            var approved = reviewWindow.ShowDialog() == true;

            if (!approved)
            {
                session.Status = ApplicationSessionStatus.Cancelled;
                session.CompletedAt = DateTime.Now;
                session.LogAction("User", "Cancelled application", true);
                await _linkedInService.CancelApplicationAsync();
                await LogApplicationSessionAsync(session);
                StatusMessage = "Application cancelled by user.";
                return;
            }

            // Step 6: User approved - submit the application
            session.Status = ApplicationSessionStatus.Submitting;
            AutoApplyProgress = "Submitting application...";
            StatusMessage = AutoApplyProgress;

            var submitted = await _linkedInService.SubmitApplicationAsync(session, progress, _autoApplyCts.Token);

            if (submitted)
            {
                session.Status = ApplicationSessionStatus.Submitted;
                session.CompletedAt = DateTime.Now;
                session.LogAction("Submit", "Application submitted successfully", true);

                // Update job status in database
                await _jobRepository.UpdateJobStatusAsync(SelectedJob.Id, ApplicationStatus.Applied);
                SelectedJob.Status = ApplicationStatus.Applied;
                SelectedJob.DateApplied = DateTime.Now;

                await LogApplicationSessionAsync(session);
                StatusMessage = $"Successfully applied to {jobTitle} at {company}!";
            }
            else
            {
                session.Status = ApplicationSessionStatus.Failed;
                session.CompletedAt = DateTime.Now;
                session.ErrorMessage = "Submission failed";
                session.LogAction("Submit", "Application submission failed", false);
                await LogApplicationSessionAsync(session);
                StatusMessage = "Application submission failed. Please try manually.";
            }
        }
        catch (OperationCanceledException)
        {
            if (session != null)
            {
                session.Status = ApplicationSessionStatus.Cancelled;
                session.CompletedAt = DateTime.Now;
                session.LogAction("System", "Operation cancelled", false);
                await LogApplicationSessionAsync(session);
            }
            await _linkedInService.CancelApplicationAsync();
            StatusMessage = "Auto-apply cancelled.";
        }
        catch (Exception ex)
        {
            if (session != null)
            {
                session.Status = ApplicationSessionStatus.Failed;
                session.CompletedAt = DateTime.Now;
                session.ErrorMessage = ex.Message;
                session.LogAction("Error", ex.Message, false);
                await LogApplicationSessionAsync(session);
            }
            await _linkedInService.CancelApplicationAsync();
            StatusMessage = $"Auto-apply error: {ex.Message}";
        }
        finally
        {
            IsAutoApplying = false;
            AutoApplyProgress = "";
        }
    }

    [RelayCommand]
    private void CancelAutoApply()
    {
        _autoApplyCts?.Cancel();
        StatusMessage = "Cancelling auto-apply...";
    }

    /// <summary>
    /// Logs an application session to a JSON file for audit and debugging.
    /// </summary>
    private async Task LogApplicationSessionAsync(ApplicationSession session)
    {
        try
        {
            var logsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JobFinder", "application-logs");

            Directory.CreateDirectory(logsDir);

            var filename = $"{session.SessionId}_{session.Status}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(logsDir, filename);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(session, options);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch
        {
            // Logging should not fail the main operation
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
        SelectedJob.SummaryCroatian = null;
        SelectedJob.ShortSummary = null;
        SelectedJob.Rating = null;
        SelectedJob.DiscardReason = null;
        StatusMessage = $"Restored '{SelectedJob.Title}'.";
    }

    [RelayCommand]
    private async Task RestoreAllJobsAsync()
    {
        // Remember the currently selected job ID so we can re-select after reload
        var selectedJobId = SelectedJob?.Id;

        var count = await _jobRepository.RestoreAllJobsAsync();
        await LoadJobsAsync();

        // Re-select the job from the new list (now with cleared summaries)
        if (selectedJobId.HasValue)
        {
            SelectedJob = Jobs.FirstOrDefault(j => j.Id == selectedJobId.Value);
        }

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
                var job = await _jobRepository.GetJobByExternalIdAsync(SelectedJob.Platform, SelectedJob.ExternalJobId);
                if (job != null)
                {
                    job.Description = details.Description;
                    job.HasEasyApply = details.HasEasyApply;
                    job.ExternalApplyUrl = details.ExternalApplyUrl;
                    job.RecruiterEmail = details.RecruiterEmail;
                    await _jobRepository.UpdateJobAsync(job);
                }

                StatusMessage = "Job details loaded. Getting AI summary...";

                // Now get AI summary if not already present
                if (_kimiService.IsConfigured && string.IsNullOrEmpty(SelectedJob.SummaryCroatian) && !string.IsNullOrEmpty(details.Description))
                {
                    try
                    {
                        var summary = await _kimiService.GetSummaryAsync(details.Description);
                        if (summary != null && !string.IsNullOrEmpty(summary.Summary))
                        {
                            SelectedJob.SummaryCroatian = summary.Summary;
                            SelectedJob.ShortSummary = summary.ShortSummary;
                            SelectedJob.Rating = summary.Rating;
                            SelectedJob.DiscardReason = summary.DiscardReason;
                            SelectedJob.AiPromptSent = summary.PromptSent;
                            SelectedJob.AiRawResponse = summary.RawResponse;

                            // Update in database
                            if (job != null)
                            {
                                job.SummaryCroatian = summary.Summary;
                                job.ShortSummary = summary.ShortSummary;
                                job.Rating = summary.Rating;
                                job.DiscardReason = summary.DiscardReason;
                                job.AiPromptSent = summary.PromptSent;
                                job.AiRawResponse = summary.RawResponse;
                                await _jobRepository.UpdateJobAsync(job);
                            }

                            StatusMessage = "Job details and AI summary loaded.";
                        }
                        else
                        {
                            StatusMessage = "Job details loaded. AI summary failed.";
                        }
                    }
                    catch
                    {
                        StatusMessage = "Job details loaded. AI summary failed.";
                    }
                }
                else if (!_kimiService.IsConfigured)
                {
                    StatusMessage = "Job details loaded. Configure Kimi API for AI summaries.";
                }
                else
                {
                    StatusMessage = "Job details loaded.";
                }
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
        // Close all platform browsers
        foreach (var service in _platformServiceFactory.GetAllServices())
        {
            await service.CloseAsync();
        }
        IsLoggedIn = false;
        StatusMessage = "All browsers closed.";
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
                        job.AiPromptSent = result.PromptSent;
                        job.AiRawResponse = result.RawResponse;

                        // Auto-discard enabled - discard jobs the AI recommends discarding
                        if (result.ShouldDiscard)
                        {
                            job.IsDiscarded = true;
                            job.DiscardReason = result.DiscardReason ?? "AI recommended discard";
                        }

                        await _jobRepository.UpdateJobAsync(job);
                        analyzed++;

                        // Update the corresponding JobViewModel in the UI collection
                        var jobVm = Jobs.FirstOrDefault(j => j.Id == job.Id);
                        if (jobVm != null)
                        {
                            jobVm.SummaryCroatian = result.Summary;
                            jobVm.ShortSummary = result.ShortSummary;
                            jobVm.Rating = result.Rating;
                            jobVm.DiscardReason = job.DiscardReason;
                            jobVm.AiPromptSent = result.PromptSent;
                            jobVm.AiRawResponse = result.RawResponse;
                            jobVm.IsDiscarded = job.IsDiscarded;
                        }

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
                        var dbJob = await _jobRepository.GetJobByExternalIdAsync(job.Platform, job.ExternalJobId);
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
                    job.AiPromptSent = result.PromptSent;
                    job.AiRawResponse = result.RawResponse;

                    // Auto-discard enabled - discard jobs the AI recommends discarding
                    if (result.ShouldDiscard)
                    {
                        job.IsDiscarded = true;
                        job.DiscardReason = result.DiscardReason ?? "AI recommended discard";
                    }

                    // Update in database
                    var dbJob = await _jobRepository.GetJobByExternalIdAsync(job.Platform, job.ExternalJobId);
                    if (dbJob != null)
                    {
                        dbJob.SummaryCroatian = result.Summary;
                        dbJob.ShortSummary = result.ShortSummary;
                        dbJob.Rating = result.Rating;
                        dbJob.DiscardReason = job.DiscardReason;
                        dbJob.IsDiscarded = job.IsDiscarded;
                        dbJob.AiPromptSent = result.PromptSent;
                        dbJob.AiRawResponse = result.RawResponse;
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
