using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using JobFinder.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace JobFinder;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = App.Services.GetRequiredService<SettingsWindow>();
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
    }

    private void JobsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel mainViewModel) return;
        if (mainViewModel.SelectedJob == null) return;

        // Make sure we clicked on a row, not on header or empty space
        // Use visual tree traversal since DataGrid elements are in visual tree, not logical tree
        DependencyObject? element = e.OriginalSource as DependencyObject;
        while (element != null && element is not DataGridRow)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        if (element is not DataGridRow) return;

        OpenJobDetailsWindow(mainViewModel);
    }

    private void OpenDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel mainViewModel) return;
        if (mainViewModel.SelectedJob == null) return;

        OpenJobDetailsWindow(mainViewModel);
    }

    private void OpenJobDetailsWindow(MainViewModel mainViewModel)
    {
        var detailsWindow = new JobDetailsWindow(mainViewModel.SelectedJob!, mainViewModel)
        {
            Owner = this
        };
        detailsWindow.Show();
    }
}
