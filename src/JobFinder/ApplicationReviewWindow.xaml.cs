using System.Globalization;
using System.Windows;
using System.Windows.Data;
using JobFinder.ViewModels;

namespace JobFinder;

/// <summary>
/// Application review dialog for human-in-the-loop approval of automated job applications.
/// </summary>
public partial class ApplicationReviewWindow : Window
{
    public ApplicationReviewWindow(ApplicationReviewViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Subscribe to close request from ViewModel
        viewModel.RequestClose += OnRequestClose;
    }

    private void OnRequestClose(object? sender, bool approved)
    {
        DialogResult = approved;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe from events
        if (DataContext is ApplicationReviewViewModel vm)
        {
            vm.RequestClose -= OnRequestClose;
        }

        base.OnClosed(e);
    }
}

/// <summary>
/// Converter that returns Visible when the value is false, Collapsed when true.
/// Inverse of BooleanToVisibilityConverter.
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility != Visibility.Visible;
        }
        return false;
    }
}
