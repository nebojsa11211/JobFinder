using CommunityToolkit.Mvvm.ComponentModel;
using JobFinder.Models;

namespace JobFinder.ViewModels;

public partial class JobViewModel : ObservableObject
{
    public int Id { get; }
    public JobPlatform Platform { get; }
    public string ExternalJobId { get; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _companyName;

    [ObservableProperty]
    private string _location;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _experienceLevel;

    [ObservableProperty]
    private string? _workplaceType;

    [ObservableProperty]
    private string? _jobUrl;

    [ObservableProperty]
    private string? _externalApplyUrl;

    [ObservableProperty]
    private string? _recruiterEmail;

    [ObservableProperty]
    private bool _hasEasyApply;

    [ObservableProperty]
    private DateTime _datePosted;

    [ObservableProperty]
    private DateTime _dateScraped;

    [ObservableProperty]
    private ApplicationStatus _status;

    [ObservableProperty]
    private DateTime? _dateApplied;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSummaryCroatian))]
    private string? _summaryCroatian;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShortSummary))]
    private string? _shortSummary;

    [ObservableProperty]
    private bool _isDiscarded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RatingDisplay))]
    [NotifyPropertyChangedFor(nameof(HasRating))]
    private int? _rating;

    [ObservableProperty]
    private string? _discardReason;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiDebugInfo))]
    private string? _aiPromptSent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiDebugInfo))]
    private string? _aiRawResponse;

    // Upwork-specific fields
    [ObservableProperty]
    private string? _budgetType;

    [ObservableProperty]
    private decimal? _hourlyRateMin;

    [ObservableProperty]
    private decimal? _hourlyRateMax;

    [ObservableProperty]
    private decimal? _fixedPriceBudget;

    [ObservableProperty]
    private string? _projectDuration;

    [ObservableProperty]
    private decimal? _clientRating;

    [ObservableProperty]
    private decimal? _clientTotalSpent;

    [ObservableProperty]
    private int? _proposalsCount;

    [ObservableProperty]
    private int? _connectsRequired;

    public string DatePostedFormatted => DatePosted.ToString("MMM dd, yyyy");
    public string DateScrapedFormatted => DateScraped.ToString("MMM dd, HH:mm");
    public string StatusDisplay => Status.ToString();
    public string PlatformDisplay => Platform.ToString();

    public bool HasExternalApply => !string.IsNullOrEmpty(ExternalApplyUrl);
    public bool HasRecruiterEmail => !string.IsNullOrEmpty(RecruiterEmail);

    public bool HasRating => Rating.HasValue;
    public string RatingDisplay => Rating.HasValue ? $"{Rating}/10" : "-";

    public bool IsUpworkJob => Platform == JobPlatform.Upwork;
    public bool IsLinkedInJob => Platform == JobPlatform.LinkedIn;

    public string BudgetDisplay => GetBudgetDisplay();

    public JobViewModel(Job job)
    {
        Id = job.Id;
        Platform = job.Platform;
        ExternalJobId = job.ExternalJobId;
        _title = job.Title;
        _companyName = job.Company?.Name ?? "Unknown";
        _location = job.Location;
        _description = job.Description;
        _experienceLevel = job.ExperienceLevel;
        _workplaceType = job.WorkplaceType;
        _jobUrl = job.JobUrl;
        _externalApplyUrl = job.ExternalApplyUrl;
        _recruiterEmail = job.RecruiterEmail;
        _hasEasyApply = job.HasEasyApply;
        _datePosted = job.DatePosted;
        _dateScraped = job.DateScraped;
        _status = job.Status;
        _dateApplied = job.DateApplied;
        _notes = job.Notes;
        _summaryCroatian = job.SummaryCroatian;
        _shortSummary = job.ShortSummary;
        _isDiscarded = job.IsDiscarded;
        _rating = job.Rating;
        _discardReason = job.DiscardReason;
        _aiPromptSent = job.AiPromptSent;
        _aiRawResponse = job.AiRawResponse;

        // Upwork-specific
        _budgetType = job.BudgetType;
        _hourlyRateMin = job.HourlyRateMin;
        _hourlyRateMax = job.HourlyRateMax;
        _fixedPriceBudget = job.FixedPriceBudget;
        _projectDuration = job.ProjectDuration;
        _clientRating = job.ClientRating;
        _clientTotalSpent = job.ClientTotalSpent;
        _proposalsCount = job.ProposalsCount;
        _connectsRequired = job.ConnectsRequired;
    }

    public bool HasSummaryCroatian => !string.IsNullOrEmpty(SummaryCroatian);
    public bool HasShortSummary => !string.IsNullOrEmpty(ShortSummary);
    public bool HasAiDebugInfo => !string.IsNullOrEmpty(AiPromptSent) || !string.IsNullOrEmpty(AiRawResponse);

    private string GetBudgetDisplay()
    {
        if (Platform != JobPlatform.Upwork) return string.Empty;

        if (BudgetType == "Hourly" && HourlyRateMin.HasValue)
        {
            return HourlyRateMax.HasValue
                ? $"${HourlyRateMin:F0}-${HourlyRateMax:F0}/hr"
                : $"${HourlyRateMin:F0}/hr";
        }

        if (BudgetType == "Fixed" && FixedPriceBudget.HasValue)
        {
            return $"${FixedPriceBudget:F0} fixed";
        }

        return "Budget not specified";
    }
}
