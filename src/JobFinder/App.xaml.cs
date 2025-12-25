using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using JobFinder.Data;
using JobFinder.Services;
using JobFinder.ViewModels;

namespace JobFinder;

public partial class App : Application
{
    private readonly ServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // Database
        services.AddDbContext<JobFinderDbContext>();

        // Services
        services.AddSingleton<ILinkedInService, LinkedInService>();
        services.AddScoped<IJobRepository, JobRepository>();

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        var viewModel = _serviceProvider.GetRequiredService<MainViewModel>();

        mainWindow.DataContext = viewModel;
        mainWindow.Show();

        // Initialize ViewModel
        await viewModel.InitializeAsync();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        var linkedInService = _serviceProvider.GetService<ILinkedInService>();
        if (linkedInService != null)
        {
            await linkedInService.CloseAsync();
        }

        _serviceProvider.Dispose();
        base.OnExit(e);
    }
}
