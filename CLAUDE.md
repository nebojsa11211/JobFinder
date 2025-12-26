# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the solution
dotnet build JobFinder.sln

# Run the application
dotnet run --project src/JobFinder/JobFinder.csproj

# Clean and rebuild
dotnet clean && dotnet build

# Install Playwright browsers (required for first run)
pwsh -Command "& {.\src\JobFinder\bin\Debug\net9.0-windows\playwright.ps1 install}"
```

## Architecture Overview

This is a WPF desktop application (.NET 9) for searching and tracking LinkedIn job applications using browser automation.

### Technology Stack
- **UI Framework**: WPF with XAML
- **MVVM Framework**: CommunityToolkit.Mvvm (source generators for `[ObservableProperty]`, `[RelayCommand]`)
- **Browser Automation**: Microsoft Playwright (Chromium)
- **Database**: SQLite via Entity Framework Core (code-first, no migrations checked in)
- **DI Container**: Microsoft.Extensions.DependencyInjection

### Project Structure

```
src/JobFinder/
├── App.xaml.cs          # DI container setup, application lifecycle
├── MainWindow.xaml      # Main UI (single-window app)
├── Data/
│   └── JobFinderDbContext.cs  # EF Core context, SQLite at %LocalAppData%/JobFinder/jobfinder.db
├── Models/
│   ├── Job.cs           # Main entity with LinkedIn job data
│   ├── SearchFilter.cs  # Search parameters
│   └── ApplicationStatus.cs  # Enum: New, Saved, Applied, Interviewing, Rejected, Ignored
├── Services/
│   ├── ILinkedInService.cs    # Browser automation interface
│   ├── LinkedInService.cs     # Playwright implementation for LinkedIn scraping
│   ├── IJobRepository.cs      # Data access interface
│   └── JobRepository.cs       # EF Core implementation
└── ViewModels/
    ├── MainViewModel.cs   # Main window logic, commands, search orchestration
    └── JobViewModel.cs    # Wrapper for Job entity with observable properties
```

### Key Design Patterns

**Dependency Injection**: Services registered in `App.xaml.cs`:
- `ILinkedInService` (Singleton) - Browser instance persists across searches
- `IJobRepository` (Scoped) - Per-operation database access
- ViewModels (Transient)

**Browser Session Persistence**: LinkedIn authentication state stored at `%LocalAppData%/JobFinder/browser-data/storage-state.json` to avoid repeated logins.

**Database Auto-Creation**: `JobFinderDbContext.OnConfiguring` uses SQLite with automatic database creation. No EF migrations are used - database schema is created from model on first run via `EnsureCreatedAsync()`.

### LinkedIn Service Flow

1. `OpenLoginWindowAsync()` - Launches headed Chromium, loads stored credentials if available
2. `CheckLoginStatusAsync()` - Verifies login by checking URL/DOM, saves session state
3. `SearchJobsAsync()` - Constructs LinkedIn search URL, scrolls job list, extracts job cards via DOM parsing
4. `GetJobDetailsAsync()` - Navigates to individual job page, extracts description and apply info
5. `StartEasyApplyAsync()` - Opens Easy Apply modal for user to complete manually
