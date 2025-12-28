using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobFinder.Models;
using JobFinder.Services;

namespace JobFinder.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    // === AI Configuration ===
    [ObservableProperty]
    private string _kimiApiKey = "";

    [ObservableProperty]
    private string _kimiApiBaseUrl = "";

    [ObservableProperty]
    private string _kimiModel = "";

    [ObservableProperty]
    private string _summaryPrompt = "";

    // === Display Options ===
    [ObservableProperty]
    private bool _showDiscardedJobs;

    [ObservableProperty]
    private bool _startBrowserMinimized;

    [ObservableProperty]
    private bool _showAiDebug;

    // === Auto-Apply Configuration ===
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProfileCharacterCount))]
    private string _userProfessionalProfile = "";

    [ObservableProperty]
    private string _coverLetterTemplate = "";

    [ObservableProperty]
    private string _applicationMessagePrompt = "";

    [ObservableProperty]
    private int _minActionDelayMs = 1500;

    [ObservableProperty]
    private int _maxActionDelayMs = 4000;

    // === Status ===
    [ObservableProperty]
    private string _statusMessage = "";

    /// <summary>
    /// Character count for professional profile display.
    /// </summary>
    public int ProfileCharacterCount => UserProfessionalProfile?.Length ?? 0;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Settings;

        // AI Configuration
        KimiApiKey = settings.KimiApiKey;
        KimiApiBaseUrl = settings.KimiApiBaseUrl;
        KimiModel = settings.KimiModel;
        SummaryPrompt = settings.SummaryPrompt;

        // Display Options
        ShowDiscardedJobs = settings.ShowDiscardedJobs;
        StartBrowserMinimized = settings.StartBrowserMinimized;
        ShowAiDebug = settings.ShowAiDebug;

        // Auto-Apply Configuration
        UserProfessionalProfile = settings.UserProfessionalProfile;
        CoverLetterTemplate = settings.CoverLetterTemplate;
        ApplicationMessagePrompt = settings.ApplicationMessagePrompt;
        MinActionDelayMs = settings.MinActionDelayMs;
        MaxActionDelayMs = settings.MaxActionDelayMs;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var settings = _settingsService.Settings;

        // AI Configuration
        settings.KimiApiKey = KimiApiKey;
        settings.KimiApiBaseUrl = KimiApiBaseUrl;
        settings.KimiModel = KimiModel;
        settings.SummaryPrompt = SummaryPrompt;

        // Display Options
        settings.ShowDiscardedJobs = ShowDiscardedJobs;
        settings.StartBrowserMinimized = StartBrowserMinimized;
        settings.ShowAiDebug = ShowAiDebug;

        // Auto-Apply Configuration
        settings.UserProfessionalProfile = UserProfessionalProfile;
        settings.CoverLetterTemplate = CoverLetterTemplate;
        settings.ApplicationMessagePrompt = ApplicationMessagePrompt;
        settings.MinActionDelayMs = MinActionDelayMs;
        settings.MaxActionDelayMs = MaxActionDelayMs;

        await _settingsService.SaveAsync();
        StatusMessage = "Settings saved!";
    }

    [RelayCommand]
    private void ResetPrompt()
    {
        SummaryPrompt = AppSettings.DefaultPrompt;
    }

    [RelayCommand]
    private void ResetCoverLetterTemplate()
    {
        CoverLetterTemplate = AppSettings.DefaultCoverLetterTemplate;
    }

    [RelayCommand]
    private void ResetApplicationPrompt()
    {
        ApplicationMessagePrompt = AppSettings.DefaultApplicationMessagePrompt;
    }
}
