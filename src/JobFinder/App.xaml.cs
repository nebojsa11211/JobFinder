using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using JobFinder.Data;
using JobFinder.Services;
using JobFinder.ViewModels;

namespace JobFinder;

public partial class App : Application
{
    private readonly ServiceProvider _serviceProvider;

    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // Database
        services.AddDbContext<JobFinderDbContext>();

        // Services
        services.AddSingleton<ILinkedInService, LinkedInService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IKimiService, KimiService>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<SettingsWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load settings first
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        await settingsService.LoadAsync();

        // Validate Kimi API key
        var kimiService = _serviceProvider.GetRequiredService<IKimiService>();
        var (isValid, errorMessage) = await kimiService.ValidateApiKeyAsync();
        if (!isValid)
        {
            MessageBox.Show(
                $"{errorMessage}\n\nAI summaries will not be available until this is resolved.",
                "Kimi AI Configuration Issue",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        var viewModel = _serviceProvider.GetRequiredService<MainViewModel>();

        mainWindow.DataContext = viewModel;
        mainWindow.Show();

        // Initialize ViewModel
        await viewModel.InitializeAsync();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            var linkedInService = _serviceProvider.GetService<ILinkedInService>();
            if (linkedInService != null)
            {
                await linkedInService.CloseAsync();
            }
            _serviceProvider.Dispose();
            base.OnExit(e);
        }
        catch (Exception ex)
        {

            throw;
        }
    }
}
