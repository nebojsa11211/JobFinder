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
    private string _company;

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

    public string DatePostedFormatted => DatePosted.ToString("MMM dd, yyyy");
    public string StatusDisplay => Status.ToString();

    public bool HasExternalApply => !string.IsNullOrEmpty(ExternalApplyUrl);
    public bool HasRecruiterEmail => !string.IsNullOrEmpty(RecruiterEmail);

    public JobViewModel(Job job)
    {
        Id = job.Id;
        LinkedInJobId = job.LinkedInJobId;
        _title = job.Title;
        _company = job.Company;
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
    }
}
