using System.Windows;

namespace JobFinder;

public partial class JobDetailsWindow : Window
{
    private readonly ViewModels.MainViewModel _mainViewModel;

    public JobDetailsWindow(ViewModels.JobViewModel job, ViewModels.MainViewModel mainViewModel)
    {
        InitializeComponent();
        DataContext = job;
        _mainViewModel = mainViewModel;

        // Update title with job info
        Title = $"{job.Title} - {job.CompanyName}";
    }

    private void CopyEmailButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.JobViewModel job && !string.IsNullOrEmpty(job.RecruiterEmail))
        {
            Clipboard.SetText(job.RecruiterEmail);
        }
    }

    private void CopySummaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.JobViewModel job && !string.IsNullOrEmpty(job.SummaryCroatian))
        {
            Clipboard.SetText(job.SummaryCroatian);
        }
    }

    private async void GetDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mainViewModel.GetJobDetailsCommand.CanExecute(null))
        {
            await _mainViewModel.GetJobDetailsCommand.ExecuteAsync(null);
        }
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mainViewModel.ApplyToJobCommand.CanExecute(null))
        {
            await _mainViewModel.ApplyToJobCommand.ExecuteAsync(null);
        }
    }

    private void OpenInBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mainViewModel.OpenJobInBrowserCommand.CanExecute(null))
        {
            _mainViewModel.OpenJobInBrowserCommand.Execute(null);
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mainViewModel.MarkAsSavedCommand.CanExecute(null))
        {
            await _mainViewModel.MarkAsSavedCommand.ExecuteAsync(null);
        }
    }

    private async void AppliedButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mainViewModel.MarkAsAppliedCommand.CanExecute(null))
        {
            await _mainViewModel.MarkAsAppliedCommand.ExecuteAsync(null);
        }
    }

    private async void IgnoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mainViewModel.MarkAsIgnoredCommand.CanExecute(null))
        {
            await _mainViewModel.MarkAsIgnoredCommand.ExecuteAsync(null);
        }
    }

    private async void DiscardButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mainViewModel.DiscardJobCommand.CanExecute(null))
        {
            await _mainViewModel.DiscardJobCommand.ExecuteAsync(null);
            Close(); // Close window after discarding
        }
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mainViewModel.RestoreJobCommand.CanExecute(null))
        {
            await _mainViewModel.RestoreJobCommand.ExecuteAsync(null);
        }
    }
}
