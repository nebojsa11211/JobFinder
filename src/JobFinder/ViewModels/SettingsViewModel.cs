using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobFinder.Models;
using JobFinder.Services;

namespace JobFinder.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string _kimiApiKey = "";

    [ObservableProperty]
    private string _kimiApiBaseUrl = "";

    [ObservableProperty]
    private string _kimiModel = "";

    [ObservableProperty]
    private string _summaryPrompt = "";

    [ObservableProperty]
    private bool _showDiscardedJobs;

    [ObservableProperty]
    private bool _startBrowserMinimized;

    [ObservableProperty]
    private string _statusMessage = "";

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Settings;
        KimiApiKey = settings.KimiApiKey;
        KimiApiBaseUrl = settings.KimiApiBaseUrl;
        KimiModel = settings.KimiModel;
        SummaryPrompt = settings.SummaryPrompt;
        ShowDiscardedJobs = settings.ShowDiscardedJobs;
        StartBrowserMinimized = settings.StartBrowserMinimized;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var settings = _settingsService.Settings;
        settings.KimiApiKey = KimiApiKey;
        settings.KimiApiBaseUrl = KimiApiBaseUrl;
        settings.KimiModel = KimiModel;
        settings.SummaryPrompt = SummaryPrompt;
        settings.ShowDiscardedJobs = ShowDiscardedJobs;
        settings.StartBrowserMinimized = StartBrowserMinimized;

        await _settingsService.SaveAsync();
        StatusMessage = "Settings saved!";
    }

    [RelayCommand]
    private void ResetPrompt()
    {
        SummaryPrompt = AppSettings.DefaultPrompt;
    }
}
