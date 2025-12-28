# JobFinder - LinkedIn Job Search & AI Analysis Application

A sophisticated WPF desktop application (.NET 9) for searching and tracking LinkedIn job applications with AI-powered analysis, browser automation, and intelligent job filtering.

---

## Table of Contents

1. [Overview](#overview)
2. [Technology Stack](#technology-stack)
3. [Architecture](#architecture)
4. [Database Schema](#database-schema)
5. [Core Features](#core-features)
6. [Services](#services)
7. [User Interface](#user-interface)
8. [Workflows](#workflows)
9. [Configuration](#configuration)
10. [Troubleshooting](#troubleshooting)

---

## Overview

JobFinder automates the tedious process of searching for .NET developer positions on LinkedIn. It combines:

- **Browser Automation**: Microsoft Playwright controls a Chromium browser to search LinkedIn and extract job data
- **AI Analysis**: Kimi K2 (Moonshot AI) analyzes job descriptions, generates Croatian summaries, rates jobs, and auto-discards irrelevant positions
- **Database Persistence**: SQLite stores all jobs, companies, and user preferences for offline access
- **Smart Filtering**: Automatically filters out non-.NET jobs (Python, Java, frontend-only, etc.)

---

## Technology Stack

| Component | Technology |
|-----------|------------|
| UI Framework | WPF with XAML |
| MVVM Framework | CommunityToolkit.Mvvm (source generators) |
| Browser Automation | Microsoft Playwright (Chromium) |
| AI Integration | Kimi K2 API (Moonshot AI) |
| Database | SQLite via Entity Framework Core |
| DI Container | Microsoft.Extensions.DependencyInjection |
| Target Framework | .NET 9.0 Windows |

---

## Architecture

### Project Structure

```
src/JobFinder/
├── App.xaml(.cs)                    # DI setup, lifecycle, API validation
├── MainWindow.xaml(.cs)             # Primary UI - job search and list
├── JobDetailsWindow.xaml(.cs)       # Detailed job view with AI summaries
├── SettingsWindow.xaml(.cs)         # Kimi AI configuration
│
├── Data/
│   └── JobFinderDbContext.cs        # EF Core context with schema migration
│
├── Models/
│   ├── Job.cs                       # Main job entity
│   ├── Company.cs                   # Company information
│   ├── ApplicationStatus.cs         # Status enum (New, Saved, Applied, etc.)
│   ├── SearchFilter.cs              # Search parameters
│   ├── AppSettings.cs               # User preferences
│   └── JobSummaryResult.cs          # AI analysis result
│
├── Services/
│   ├── ILinkedInService.cs          # Browser automation interface
│   ├── LinkedInService.cs           # Playwright implementation
│   ├── IKimiService.cs              # AI service interface
│   ├── KimiService.cs               # Kimi K2 implementation
│   ├── IJobRepository.cs            # Job data access interface
│   ├── JobRepository.cs             # EF Core implementation
│   ├── ICompanyRepository.cs        # Company data access interface
│   ├── CompanyRepository.cs         # EF Core implementation
│   ├── ISettingsService.cs          # Settings interface
│   └── SettingsService.cs           # JSON persistence
│
└── ViewModels/
    ├── MainViewModel.cs             # Main window logic
    ├── JobViewModel.cs              # Observable job wrapper
    └── SettingsViewModel.cs         # Settings dialog logic
```

### Dependency Injection

```csharp
// Singletons - persist for app lifetime
ILinkedInService    → LinkedInService     // Browser instance
ISettingsService    → SettingsService     // Shared settings
IKimiService        → KimiService         // HTTP client

// Scoped - per operation
IJobRepository      → JobRepository       // Database access
ICompanyRepository  → CompanyRepository   // Database access

// Transient - new per request
MainViewModel, SettingsViewModel, Windows
```

---

## Database Schema

**Location**: `%LocalAppData%/JobFinder/jobfinder.db`

### Jobs Table

| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER | Primary key |
| LinkedInJobId | TEXT | Unique LinkedIn job identifier |
| Title | TEXT | Job title |
| CompanyId | INTEGER | Foreign key to Companies |
| Location | TEXT | Job location |
| Description | TEXT | Full job description |
| ExperienceLevel | TEXT | e.g., "Mid-Senior level" |
| WorkplaceType | TEXT | e.g., "Remote", "On-site" |
| JobUrl | TEXT | Full LinkedIn job URL |
| ExternalApplyUrl | TEXT | External application URL |
| RecruiterEmail | TEXT | Extracted from description |
| HasEasyApply | BOOLEAN | LinkedIn Easy Apply available |
| DatePosted | DATETIME | When job was posted |
| DateScraped | DATETIME | When job was scraped |
| Status | TEXT | ApplicationStatus enum |
| DateApplied | DATETIME | When user applied |
| Notes | TEXT | User notes |
| SummaryCroatian | TEXT | AI summary (3-5 bullets) |
| ShortSummary | TEXT | AI summary (5-10 words) |
| Rating | INTEGER | AI rating 1-10 |
| IsDiscarded | BOOLEAN | Discarded flag |
| DiscardReason | TEXT | Why discarded |
| AiPromptSent | TEXT | Debug: prompt sent |
| AiRawResponse | TEXT | Debug: raw AI response |

### Companies Table

| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER | Primary key |
| Name | TEXT | Company name |
| LinkedInId | TEXT | LinkedIn company ID |
| LinkedInUrl | TEXT | Company profile URL |
| Website | TEXT | Company website |
| Industry | TEXT | Industry classification |
| Size | TEXT | Employee count range |
| Headquarters | TEXT | HQ location |
| Description | TEXT | Company description |
| LogoUrl | TEXT | Company logo URL |
| Notes | TEXT | User notes |
| IsBlacklisted | BOOLEAN | Auto-discard jobs |
| IsFavorite | BOOLEAN | Marked as favorite |

---

## Core Features

### 1. LinkedIn Job Search

- **Automated Search**: Playwright browser navigates LinkedIn with your filters
- **Session Persistence**: Login state saved to avoid repeated authentication
- **Configurable Filters**:
  - Job title/keywords (default: ".NET Developer")
  - Location (default: "Croatia")
  - Experience levels (Mid-Senior, Senior)
  - Remote only option
  - Maximum results limit

### 2. AI-Powered Job Analysis

The Kimi K2 AI analyzes each job description and returns:

| Field | Description |
|-------|-------------|
| ShortSummary | 5-10 word summary in Croatian |
| Summary | 3-5 bullet points in Croatian |
| Rating | 1-10 score based on match criteria |
| ShouldDiscard | Boolean recommendation |
| DiscardReason | Explanation if discarding |

**Rating Criteria**:
- Clear, specific requirements (not vague)
- Reasonable experience expectations
- Benefits/compensation mentioned
- Remote/flexible work options
- Strong .NET/C# tech stack focus
- Freelance/contract opportunities (bonus)

### 3. Auto-Discard Logic

Jobs are automatically discarded if they:

- Don't require .NET, C#, ASP.NET Core, Blazor, WPF, MAUI, or Azure as **primary** technology
- Are DevOps/SRE/Infrastructure roles without C#/.NET coding
- Are frontend-only (React, Angular, Vue without .NET backend)
- Require Python, Java, Node.js, Ruby, Go, or Rust only
- Are Data Engineer/ML roles without .NET
- Are generic recruiter spam or unrelated postings
- Have unrealistic experience requirements

**Tech Detection Patterns**:
```
.NET indicators: .net, c#, csharp, asp.net, blazor, wpf, maui,
                 winforms, xamarin, entity framework, ef core,
                 nuget, azure functions, visual studio

Other stacks:   python, java, ruby, php, go, rust, node.js,
                react, angular, vue, devops, aws, gcp
```

### 4. Application Status Tracking

| Status | Description |
|--------|-------------|
| New | Just found, not reviewed |
| Saved | Bookmarked for later |
| Applied | User has applied |
| Interviewing | In interview process |
| Rejected | Not selected |
| Ignored | User doesn't want this job |

### 5. Batch Operations

- **Fetch Missing Details**: Auto-fetches descriptions for all jobs
- **Fetch AI Summaries**: Analyzes all jobs without summaries
- **Re-analyze All**: Re-runs AI on all jobs with updated prompt
- **Restore All**: Restores all discarded jobs for re-analysis

---

## Services

### LinkedInService

Automates LinkedIn using Microsoft Playwright.

**Key Methods**:

| Method | Description |
|--------|-------------|
| `OpenLoginWindowAsync()` | Opens browser, loads saved session |
| `CheckLoginStatusAsync()` | Verifies login, saves session state |
| `SearchJobsAsync()` | Searches LinkedIn with filters, extracts job cards |
| `GetJobDetailsAsync()` | Fetches full job description from job page |
| `StartEasyApplyAsync()` | Opens Easy Apply modal for user completion |
| `CloseAsync()` | Saves session, closes browser |

**Session Storage**: `%LocalAppData%/JobFinder/browser-data/storage-state.json`

### KimiService

Integrates with Moonshot AI's Kimi K2 model.

**Configuration**:
- API Base URL: `https://api.moonshot.ai/v1`
- Model: `kimi-k2-0711-preview`
- Temperature: 0.3 (deterministic)
- Response Format: JSON

**Key Methods**:

| Method | Description |
|--------|-------------|
| `GetSummaryAsync()` | Analyzes job, returns structured result |
| `ValidateApiKeyAsync()` | Tests API key on startup |
| `IsConfigured` | Returns true if API key is set |

**Response Parsing**:
- Multiple JSON property name variants supported
- Fallback to plain text parsing if JSON fails
- Parse failures marked (jobs won't auto-discard)
- Responses logged to `%LocalAppData%/JobFinder/ai-logs/`

### JobRepository

Data access for Job entities.

| Method | Description |
|--------|-------------|
| `GetAllJobsAsync()` | Get all jobs (optionally include discarded) |
| `GetJobsByStatusAsync()` | Filter by application status |
| `GetActiveJobsAsync()` | Non-discarded jobs only |
| `AddJobsAsync()` | Batch insert with deduplication |
| `UpdateJobStatusAsync()` | Update status, set DateApplied |
| `DiscardJobAsync()` | Mark as discarded |
| `RestoreJobAsync()` | Restore, clear summaries |
| `RestoreAllJobsAsync()` | Batch restore all |

### CompanyRepository

Data access for Company entities.

| Method | Description |
|--------|-------------|
| `GetOrCreateCompanyAsync()` | Get existing or create new |
| `GetBlacklistedCompaniesAsync()` | Companies to auto-skip |
| `SetBlacklistedAsync()` | Toggle blacklist flag |
| `SetFavoriteAsync()` | Toggle favorite flag |

---

## User Interface

### MainWindow

**Layout**: 3-column grid

| Section | Contents |
|---------|----------|
| **Left Panel** | Search filters, refresh button, status filters |
| **Center Panel** | Job list DataGrid with rating, status badges |
| **Right Actions** | Restore All, Re-analyze All, Open Details |
| **Status Bar** | Progress messages, loading indicator |

**Job List Columns**:
- Title (with short AI summary)
- Company
- Location
- Rating (1-10)
- Scraped date
- Posted date
- Status (color-coded badge)
- Easy Apply indicator

### JobDetailsWindow

**Sections**:
1. **Header**: Title, company, location, status/rating badges
2. **Recruiter Email**: If found, with copy button
3. **AI Summary**: Croatian bullet points with copy button
4. **Description**: Full job description
5. **AI Debug**: (optional) Prompt sent and raw response
6. **Actions**: Apply, Open in Browser, Save, Mark Applied, Ignore, Discard/Restore

### SettingsWindow

**Configuration Options**:
- Kimi API Key (password field)
- API Base URL
- Model name
- Summary prompt template (with Reset button)
- Show discarded jobs checkbox
- Start browser minimized checkbox
- Show AI debug info checkbox

---

## Workflows

### Job Search Workflow

```
1. User clicks "Refresh"
2. Browser opens (if not already open)
3. Loads saved session or user logs in manually
4. LinkedInService searches with filters
5. Extracts job cards from search results
6. Saves new jobs to database (skips duplicates)
7. Fetches full descriptions for jobs without them
8. Sends descriptions to Kimi AI for analysis
9. Auto-discards non-.NET jobs
10. Updates UI with ratings and summaries
```

### AI Analysis Workflow

```
1. Job description sent to Kimi K2 API
2. AI returns JSON with summary, rating, discard recommendation
3. KimiService validates .NET tech requirement
4. If no .NET tech found, overrides to discard
5. Job updated in database
6. If ShouldDiscard=true, job marked IsDiscarded=true
7. Discarded jobs hidden from main list
```

### Application Tracking Workflow

```
1. User reviews job in details window
2. Clicks "Apply to Job":
   - Easy Apply: Opens modal in browser
   - External: Opens external URL
   - Fallback: Opens job page in browser
3. User clicks "Mark Applied"
4. Status changed to Applied, DateApplied set
5. Job appears in "Applied" filter
```

---

## Configuration

### Settings File

**Location**: `%LocalAppData%/JobFinder/settings.json`

```json
{
  "KimiApiKey": "your-api-key",
  "KimiApiBaseUrl": "https://api.moonshot.ai/v1",
  "KimiModel": "kimi-k2-0711-preview",
  "SummaryPrompt": "...",
  "ShowDiscardedJobs": false,
  "StartBrowserMinimized": false,
  "ShowAiDebug": false
}
```

### Default AI Prompt

The default prompt instructs Kimi to:

1. Create a SHORT summary (5-10 words max) in Croatian
2. Generate 3-5 Croatian bullet points (responsibilities, skills, benefits)
3. Rate 1-10 based on match criteria
4. Determine if job should be auto-discarded
5. Return structured JSON

### File Locations

| File | Location |
|------|----------|
| Settings | `%LocalAppData%/JobFinder/settings.json` |
| Database | `./jobfinder.db` |
| Browser Session | `%LocalAppData%/JobFinder/browser-data/storage-state.json` |
| AI Logs | `%LocalAppData%/JobFinder/ai-logs/{timestamp}_{status}.log` |

---

## Troubleshooting

### Kimi API Issues

| Problem | Solution |
|---------|----------|
| "Invalid API key" | Check API key in Settings |
| "API endpoint not found" | Verify API base URL |
| "Rate limit exceeded" | Wait and retry, reduce batch size |
| Parse failures | Check AI logs, prompt may need adjustment |

### LinkedIn Issues

| Problem | Solution |
|---------|----------|
| Not logging in | Delete storage-state.json, re-authenticate |
| Limited results | Ensure you're logged in (green indicator) |
| Session expired | Close browser, click Refresh to re-login |
| Captcha shown | Complete captcha manually in browser |

### Database Issues

| Problem | Solution |
|---------|----------|
| Corruption | Delete jobfinder.db, restart app |
| Schema errors | App auto-migrates, check console for errors |
| Duplicate jobs | By design - LinkedInJobId is unique |

### Search Issues

| Problem | Solution |
|---------|----------|
| No results | Broaden filters (location, experience) |
| Wrong jobs | Check job title keywords |
| Missing descriptions | Click "Refresh" to fetch details |
| No AI summaries | Ensure Kimi API key is configured |

---

## Build & Run

```bash
# Build the solution
dotnet build JobFinder.sln

# Run the application
dotnet run --project src/JobFinder/JobFinder.csproj

# Install Playwright browsers (first run only)
pwsh -Command "& {.\src\JobFinder\bin\Debug\net9.0-windows\playwright.ps1 install}"
```

---

## Summary

JobFinder streamlines the .NET job search process by:

1. **Automating LinkedIn** - No more manual searching and scrolling
2. **AI-Powered Filtering** - Automatically discards non-.NET jobs
3. **Croatian Summaries** - Quick job understanding in your language
4. **Smart Ratings** - 1-10 scores help prioritize applications
5. **Status Tracking** - Never lose track of where you applied
6. **Offline Access** - All data stored locally in SQLite
7. **Session Persistence** - Login once, search many times

The application is designed specifically for .NET developers looking for remote positions, with intelligent filtering that saves hours of manual job review.
