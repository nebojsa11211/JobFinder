using CommunityToolkit.Mvvm.ComponentModel;
using JobFinder.Models;

namespace JobFinder.ViewModels;

public partial class JobViewModel : ObservableObject
{
    public int Id { get; }
    public string LinkedInJobId { get; }

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

    public string DatePostedFormatted => DatePosted.ToString("MMM dd, yyyy");
    public string DateScrapedFormatted => DateScraped.ToString("MMM dd, HH:mm");
    public string StatusDisplay => Status.ToString();

    public bool HasExternalApply => !string.IsNullOrEmpty(ExternalApplyUrl);
    public bool HasRecruiterEmail => !string.IsNullOrEmpty(RecruiterEmail);

    public bool HasRating => Rating.HasValue;
    public string RatingDisplay => Rating.HasValue ? $"{Rating}/10" : "-";

    public JobViewModel(Job job)
    {
        Id = job.Id;
        LinkedInJobId = job.LinkedInJobId;
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
    }

    public bool HasSummaryCroatian => !string.IsNullOrEmpty(SummaryCroatian);
    public bool HasShortSummary => !string.IsNullOrEmpty(ShortSummary);
}
