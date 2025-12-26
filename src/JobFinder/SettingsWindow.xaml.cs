using System.Windows;
using JobFinder.ViewModels;

namespace JobFinder;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        // Load API key into password box
        Loaded += (s, e) => ApiKeyBox.Password = _viewModel.KimiApiKey;
        ApiKeyBox.PasswordChanged += (s, e) => _viewModel.KimiApiKey = ApiKeyBox.Password;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Save command is already bound, just close after a delay to show message
        Dispatcher.BeginInvoke(async () =>
        {
            await Task.Delay(500);
            Close();
        });
    }
}
