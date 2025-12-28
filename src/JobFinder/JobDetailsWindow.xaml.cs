using System.Runtime.InteropServices;
using System.Windows;
using JobFinder.Services;

namespace JobFinder;

public partial class JobDetailsWindow : Window
{
    private readonly ViewModels.MainViewModel _mainViewModel;
    private readonly ISettingsService _settingsService;

    public bool ShowAiDebug => _settingsService?.Settings.ShowAiDebug ?? false;

    // Win32 clipboard APIs
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    public JobDetailsWindow(ViewModels.JobViewModel job, ViewModels.MainViewModel mainViewModel, ISettingsService settingsService)
    {
        _settingsService = settingsService;
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
            CopyToClipboard(job.RecruiterEmail);
        }
    }

    private void CopySummaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.JobViewModel job)
        {
            if (!string.IsNullOrEmpty(job.SummaryCroatian))
            {
                CopyToClipboard(job.SummaryCroatian);
                MessageBox.Show("Summary copied!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Summary is empty!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else
        {
            MessageBox.Show("No job selected!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CopyToClipboard(string text)
    {
        for (int i = 0; i < 10; i++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    EmptyClipboard();

                    var bytes = (text.Length + 1) * 2; // Unicode = 2 bytes per char + null terminator
                    var hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
                    if (hGlobal == IntPtr.Zero)
                        throw new Exception("GlobalAlloc failed");

                    var target = GlobalLock(hGlobal);
                    if (target == IntPtr.Zero)
                        throw new Exception("GlobalLock failed");

                    try
                    {
                        Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                        Marshal.WriteInt16(target, text.Length * 2, 0); // null terminator
                    }
                    finally
                    {
                        GlobalUnlock(hGlobal);
                    }

                    if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
                        throw new Exception("SetClipboardData failed");

                    return; // Success
                }
                finally
                {
                    CloseClipboard();
                }
            }
            Thread.Sleep(50);
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
