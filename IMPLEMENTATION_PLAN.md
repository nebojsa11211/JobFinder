# Implementation Plan: AI-Powered Automated Job Applications

## Overview

This plan details the implementation of an intelligent, human-in-the-loop automated job application system for the JobFinder WPF application. The system will use AI to generate personalized application messages, automate LinkedIn Easy Apply forms, and require human approval before final submission.

---

## Architecture Decisions

### Safety-First Design Principles

1. **Human-in-the-Loop is Mandatory**: No application is ever submitted without explicit user approval
2. **Full Transparency**: User sees exactly what will be submitted before approval
3. **Editable Content**: All AI-generated content can be modified by the user
4. **Complete Logging**: Every interaction is logged for debugging and accountability
5. **Anti-Detection**: Human-like delays and behavior patterns to avoid account flagging

### New Components

```
Models/
├── EasyApplyQuestion.cs          # Question/answer model for form fields
├── ApplicationSession.cs         # Full application session data for logging
└── ApplicationMessageResult.cs   # AI-generated message result

ViewModels/
└── ApplicationReviewViewModel.cs # Review dialog logic

Views/
└── ApplicationReviewWindow.xaml  # Human approval dialog

Services/
├── IKimiService.cs              # Extended with message generation
├── KimiService.cs               # Message generation implementation
├── ILinkedInService.cs          # Extended with application methods
└── LinkedInService.cs           # Easy Apply automation
```

### Modified Components

```
Models/
└── AppSettings.cs               # +UserProfessionalProfile, +CoverLetterTemplate, +ApplicationPrompt

Views/
└── SettingsWindow.xaml          # +Profile section, +Cover letter template

ViewModels/
├── SettingsViewModel.cs         # +New properties for profile/template
└── MainViewModel.cs             # +AutoApplyCommand with review flow
```

---

## Phase 1: User Profile & Settings Management

### 1.1 Update AppSettings.cs

Add new properties:

```csharp
/// <summary>
/// User's professional profile (CV content, skills, experience).
/// Used by AI to generate personalized application messages.
/// </summary>
public string UserProfessionalProfile { get; set; } = "";

/// <summary>
/// Cover letter template with placeholders.
/// Placeholders: [JobTitle], [Company], [Skills], [Experience]
/// </summary>
public string CoverLetterTemplate { get; set; } = DefaultCoverLetterTemplate;

/// <summary>
/// Prompt template for generating application messages.
/// Placeholders: {profile}, {jobDescription}, {jobTitle}, {company}
/// </summary>
public string ApplicationPrompt { get; set; } = DefaultApplicationPrompt;

/// <summary>
/// Minimum delay between automated actions (milliseconds).
/// </summary>
public int MinActionDelayMs { get; set; } = 1500;

/// <summary>
/// Maximum delay between automated actions (milliseconds).
/// </summary>
public int MaxActionDelayMs { get; set; } = 4000;

public const string DefaultCoverLetterTemplate = @"...";
public const string DefaultApplicationPrompt = @"...";
```

### 1.2 Update SettingsWindow.xaml

Add new sections:

1. **Professional Profile Section**
   - Large TextBox for pasting CV content
   - Label explaining this is used for AI personalization
   - Character count indicator

2. **Cover Letter Template Section**
   - TextBox with placeholder documentation
   - Reset to default button

3. **Application Settings Section**
   - Min/Max delay sliders for anti-bot behavior
   - Application prompt template (advanced, collapsible)

### 1.3 Update SettingsViewModel.cs

Add new observable properties:
- `UserProfessionalProfile`
- `CoverLetterTemplate`
- `ApplicationPrompt`
- `MinActionDelayMs`
- `MaxActionDelayMs`

Add commands:
- `ResetCoverLetterTemplateCommand`
- `ResetApplicationPromptCommand`

---

## Phase 2: AI Message Generation

### 2.1 Create ApplicationMessageResult.cs

```csharp
namespace JobFinder.Models;

public class ApplicationMessageResult
{
    /// <summary>
    /// The personalized cover letter/message to send with application.
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Key points extracted from job description that were addressed.
    /// </summary>
    public List<string> AddressedRequirements { get; set; } = [];

    /// <summary>
    /// Skills from user profile that match the job requirements.
    /// </summary>
    public List<string> MatchingSkills { get; set; } = [];

    /// <summary>
    /// Confidence score (0-100) of how well the message matches the job.
    /// </summary>
    public int ConfidenceScore { get; set; }

    /// <summary>
    /// Whether parsing failed (fallback to template).
    /// </summary>
    public bool ParseFailed { get; set; }

    /// <summary>
    /// Raw AI response for debugging.
    /// </summary>
    public string? RawResponse { get; set; }
}
```

### 2.2 Extend IKimiService.cs

Add new method:

```csharp
/// <summary>
/// Generates a personalized application message based on job and user profile.
/// </summary>
Task<ApplicationMessageResult?> GenerateApplicationMessageAsync(
    string jobDescription,
    string jobTitle,
    string company,
    string userProfile,
    CancellationToken cancellationToken = default);

/// <summary>
/// Generates answers for Easy Apply questions based on user profile.
/// </summary>
Task<Dictionary<string, string>> GenerateQuestionAnswersAsync(
    List<EasyApplyQuestion> questions,
    string userProfile,
    string jobDescription,
    CancellationToken cancellationToken = default);
```

### 2.3 Implement in KimiService.cs

**Message Generation Prompt Design:**

```
You are a professional job application assistant. Generate a concise, personalized cover letter message.

USER PROFILE:
{profile}

JOB TITLE: {jobTitle}
COMPANY: {company}
JOB DESCRIPTION:
{jobDescription}

INSTRUCTIONS:
1. Write a professional, concise message (150-250 words max)
2. Address 2-3 specific requirements from the job description
3. Highlight relevant experience from the user's profile
4. Be genuine and avoid generic phrases
5. End with enthusiasm for the opportunity

Respond with JSON:
{
  "message": "The cover letter text...",
  "addressedRequirements": ["requirement1", "requirement2"],
  "matchingSkills": ["skill1", "skill2"],
  "confidenceScore": 85
}
```

**Question Answering Prompt Design:**

```
Answer these job application questions based on the candidate's profile.

PROFILE:
{profile}

JOB CONTEXT:
{jobDescription}

QUESTIONS:
1. {question1}
2. {question2}
...

For each question, provide a concise, honest answer.
If the profile doesn't contain the information, provide a reasonable professional answer.

Respond with JSON:
{
  "answers": {
    "question1": "answer1",
    "question2": "answer2"
  }
}
```

---

## Phase 3: LinkedIn Easy Apply Automation

### 3.1 Create EasyApplyQuestion.cs

```csharp
namespace JobFinder.Models;

public class EasyApplyQuestion
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The question text as displayed in the form.
    /// </summary>
    public string QuestionText { get; set; } = "";

    /// <summary>
    /// Type of input: Text, TextArea, Select, Radio, Checkbox, Number
    /// </summary>
    public QuestionType Type { get; set; }

    /// <summary>
    /// Available options for Select/Radio questions.
    /// </summary>
    public List<string> Options { get; set; } = [];

    /// <summary>
    /// Whether the question is required.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// The AI-generated or user-provided answer.
    /// </summary>
    public string Answer { get; set; } = "";

    /// <summary>
    /// Playwright selector for the input element.
    /// </summary>
    public string Selector { get; set; } = "";
}

public enum QuestionType
{
    Text,
    TextArea,
    Select,
    Radio,
    Checkbox,
    Number,
    YesNo,
    Unknown
}
```

### 3.2 Create ApplicationSession.cs

```csharp
namespace JobFinder.Models;

public class ApplicationSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public int JobId { get; set; }
    public string LinkedInJobId { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string Company { get; set; } = "";
    public DateTime StartedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// The message that was/will be submitted.
    /// </summary>
    public string ApplicationMessage { get; set; } = "";

    /// <summary>
    /// All questions detected and their answers.
    /// </summary>
    public List<EasyApplyQuestion> Questions { get; set; } = [];

    /// <summary>
    /// Step-by-step log of all actions taken.
    /// </summary>
    public List<ApplicationAction> Actions { get; set; } = [];

    /// <summary>
    /// Final status: Pending, Approved, Submitted, Failed, Cancelled
    /// </summary>
    public ApplicationSessionStatus Status { get; set; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

public class ApplicationAction
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string ActionType { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Success { get; set; }
    public string? Details { get; set; }
}

public enum ApplicationSessionStatus
{
    Pending,      // Preparing application
    ReadyForReview, // Waiting for user approval
    Approved,     // User approved, ready to submit
    Submitting,   // Currently submitting
    Submitted,    // Successfully submitted
    Failed,       // Submission failed
    Cancelled     // User cancelled
}
```

### 3.3 Extend ILinkedInService.cs

Add new methods:

```csharp
/// <summary>
/// Prepares an Easy Apply application without submitting.
/// Opens the modal, detects form fields, and returns questions.
/// </summary>
Task<ApplicationSession?> PrepareApplicationAsync(
    Job job,
    IProgress<string>? progress = null,
    CancellationToken cancellationToken = default);

/// <summary>
/// Fills in the Easy Apply form with provided answers and submits.
/// Only call after user approval.
/// </summary>
Task<bool> SubmitApplicationAsync(
    ApplicationSession session,
    IProgress<string>? progress = null,
    CancellationToken cancellationToken = default);

/// <summary>
/// Cancels an in-progress application (closes modal without submitting).
/// </summary>
Task CancelApplicationAsync();
```

### 3.4 Implement in LinkedInService.cs

**PrepareApplicationAsync Implementation:**

```csharp
public async Task<ApplicationSession?> PrepareApplicationAsync(
    Job job,
    IProgress<string>? progress = null,
    CancellationToken cancellationToken = default)
{
    var session = new ApplicationSession
    {
        JobId = job.Id,
        LinkedInJobId = job.LinkedInJobId,
        JobTitle = job.Title,
        Company = job.Company?.Name ?? "Unknown"
    };

    try
    {
        // 1. Navigate to job page with human-like delay
        await HumanDelay();
        progress?.Report("Opening job page...");
        await _page.GotoAsync(job.JobUrl);
        await HumanDelay();

        // 2. Click Easy Apply button
        progress?.Report("Opening Easy Apply form...");
        var easyApplyButton = await FindEasyApplyButton();
        if (easyApplyButton == null)
        {
            session.Status = ApplicationSessionStatus.Failed;
            session.ErrorMessage = "Easy Apply button not found";
            return session;
        }

        await HumanDelay(500, 1500);
        await easyApplyButton.ClickAsync();
        await HumanDelay();

        // 3. Wait for modal to appear
        await _page.WaitForSelectorAsync(".jobs-easy-apply-modal",
            new() { Timeout = 5000 });

        // 4. Detect all form fields and questions across all pages
        progress?.Report("Analyzing application form...");
        session.Questions = await DetectAllQuestionsAsync(progress, cancellationToken);

        session.Status = ApplicationSessionStatus.ReadyForReview;
        session.Actions.Add(new ApplicationAction
        {
            ActionType = "FormAnalyzed",
            Description = $"Detected {session.Questions.Count} questions",
            Success = true
        });

        return session;
    }
    catch (Exception ex)
    {
        session.Status = ApplicationSessionStatus.Failed;
        session.ErrorMessage = ex.Message;
        return session;
    }
}
```

**Question Detection Logic:**

```csharp
private async Task<List<EasyApplyQuestion>> DetectAllQuestionsAsync(
    IProgress<string>? progress,
    CancellationToken cancellationToken)
{
    var allQuestions = new List<EasyApplyQuestion>();
    var pageCount = 0;

    while (!cancellationToken.IsCancellationRequested)
    {
        pageCount++;
        progress?.Report($"Analyzing form page {pageCount}...");

        // Detect questions on current page
        var pageQuestions = await DetectPageQuestionsAsync();
        allQuestions.AddRange(pageQuestions);

        // Check for Next button
        var nextButton = await _page.QuerySelectorAsync(
            "button[aria-label='Continue to next step'], " +
            "button:has-text('Next'), " +
            "button:has-text('Review')");

        if (nextButton == null)
            break; // We're on the final page

        // Check if it's the Submit button (final page)
        var buttonText = await nextButton.InnerTextAsync();
        if (buttonText.Contains("Submit", StringComparison.OrdinalIgnoreCase))
            break;

        // Move to next page (but don't fill anything yet)
        await HumanDelay(300, 800);
        await nextButton.ClickAsync();
        await HumanDelay();
    }

    // Go back to first page for filling
    // (LinkedIn remembers form state)

    return allQuestions;
}
```

**Human-Like Behavior Implementation:**

```csharp
private readonly Random _random = new();

private async Task HumanDelay(int? minMs = null, int? maxMs = null)
{
    var settings = _settingsService.Settings;
    var min = minMs ?? settings.MinActionDelayMs;
    var max = maxMs ?? settings.MaxActionDelayMs;

    var delay = _random.Next(min, max);

    // Occasionally add extra "thinking" time
    if (_random.Next(100) < 15) // 15% chance
    {
        delay += _random.Next(500, 2000);
    }

    await Task.Delay(delay);
}

private async Task HumanTypeAsync(IElementHandle element, string text)
{
    // Type character by character with varying delays
    foreach (var c in text)
    {
        await element.TypeAsync(c.ToString());
        await Task.Delay(_random.Next(30, 120)); // Human typing speed
    }
}

private async Task HumanScrollAsync()
{
    // Random scroll amount
    var scrollAmount = _random.Next(100, 300);
    await _page.EvaluateAsync($"window.scrollBy(0, {scrollAmount})");
    await Task.Delay(_random.Next(200, 500));
}
```

**SubmitApplicationAsync Implementation:**

```csharp
public async Task<bool> SubmitApplicationAsync(
    ApplicationSession session,
    IProgress<string>? progress = null,
    CancellationToken cancellationToken = default)
{
    try
    {
        session.Status = ApplicationSessionStatus.Submitting;

        // 1. Fill in all answers page by page
        var currentPage = 0;

        foreach (var question in session.Questions)
        {
            progress?.Report($"Filling: {question.QuestionText.Substring(0, Math.Min(50, question.QuestionText.Length))}...");

            await HumanDelay(500, 1200);
            await FillQuestionAsync(question);

            session.Actions.Add(new ApplicationAction
            {
                ActionType = "FieldFilled",
                Description = question.QuestionText,
                Details = question.Answer,
                Success = true
            });
        }

        // 2. Navigate through pages and fill
        // (Implementation details for multi-page forms)

        // 3. Find and click Submit button
        progress?.Report("Submitting application...");
        var submitButton = await _page.QuerySelectorAsync(
            "button[aria-label='Submit application'], " +
            "button:has-text('Submit application')");

        if (submitButton == null)
        {
            session.Status = ApplicationSessionStatus.Failed;
            session.ErrorMessage = "Submit button not found";
            return false;
        }

        await HumanDelay(1000, 2000);
        await submitButton.ClickAsync();

        // 4. Wait for confirmation
        await Task.Delay(2000);

        // Check for success indicator
        var successModal = await _page.QuerySelectorAsync(
            ".artdeco-modal:has-text('Application sent'), " +
            "[data-test-modal-id='post-apply-modal']");

        if (successModal != null)
        {
            session.Status = ApplicationSessionStatus.Submitted;
            session.CompletedAt = DateTime.Now;

            // Dismiss the modal
            var closeButton = await _page.QuerySelectorAsync(
                ".artdeco-modal button[aria-label='Dismiss']");
            if (closeButton != null)
            {
                await HumanDelay(500, 1000);
                await closeButton.ClickAsync();
            }

            return true;
        }

        session.Status = ApplicationSessionStatus.Failed;
        session.ErrorMessage = "Submission confirmation not detected";
        return false;
    }
    catch (Exception ex)
    {
        session.Status = ApplicationSessionStatus.Failed;
        session.ErrorMessage = ex.Message;
        return false;
    }
}
```

---

## Phase 4: Human-in-the-Loop Review UI

### 4.1 Create ApplicationReviewViewModel.cs

```csharp
namespace JobFinder.ViewModels;

public partial class ApplicationReviewViewModel : ObservableObject
{
    private readonly IKimiService _kimiService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string _jobTitle = "";

    [ObservableProperty]
    private string _company = "";

    [ObservableProperty]
    private string _applicationMessage = "";

    [ObservableProperty]
    private ObservableCollection<QuestionAnswerViewModel> _questions = [];

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private int _confidenceScore;

    [ObservableProperty]
    private List<string> _matchingSkills = [];

    public ApplicationSession Session { get; private set; }

    public bool IsApproved { get; private set; }

    [RelayCommand]
    private async Task RegenerateMessageAsync()
    {
        // Regenerate AI message with same inputs
    }

    [RelayCommand]
    private void Approve()
    {
        // Update session with edited values
        Session.ApplicationMessage = ApplicationMessage;
        foreach (var q in Questions)
        {
            var sessionQ = Session.Questions.FirstOrDefault(x => x.Id == q.Id);
            if (sessionQ != null)
                sessionQ.Answer = q.Answer;
        }

        Session.Status = ApplicationSessionStatus.Approved;
        IsApproved = true;
        // Close window via event/messenger
    }

    [RelayCommand]
    private void Cancel()
    {
        Session.Status = ApplicationSessionStatus.Cancelled;
        IsApproved = false;
        // Close window
    }
}

public partial class QuestionAnswerViewModel : ObservableObject
{
    public string Id { get; set; }

    [ObservableProperty]
    private string _questionText = "";

    [ObservableProperty]
    private string _answer = "";

    [ObservableProperty]
    private QuestionType _questionType;

    [ObservableProperty]
    private List<string> _options = [];

    [ObservableProperty]
    private bool _isRequired;
}
```

### 4.2 Create ApplicationReviewWindow.xaml

**Layout Structure:**

```xml
<Window Title="Review Application - {JobTitle} at {Company}">
    <Grid>
        <!-- Header: Job info and confidence score -->
        <Border Background="{StaticResource PrimaryBrush}">
            <StackPanel>
                <TextBlock Text="{Binding JobTitle}" FontSize="20"/>
                <TextBlock Text="{Binding Company}"/>
                <StackPanel Orientation="Horizontal">
                    <TextBlock Text="Match Confidence:"/>
                    <TextBlock Text="{Binding ConfidenceScore}"/>
                    <TextBlock Text="%"/>
                </StackPanel>
            </StackPanel>
        </Border>

        <!-- Main content: Scrollable -->
        <ScrollViewer>
            <StackPanel>
                <!-- Application Message Section -->
                <GroupBox Header="Application Message (Editable)">
                    <Grid>
                        <TextBox Text="{Binding ApplicationMessage}"
                                 AcceptsReturn="True"
                                 Height="200"/>
                        <Button Content="Regenerate"
                                Command="{Binding RegenerateMessageCommand}"/>
                    </Grid>
                </GroupBox>

                <!-- Matching Skills -->
                <GroupBox Header="Skills Highlighted">
                    <ItemsControl ItemsSource="{Binding MatchingSkills}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Border Background="LightGreen" CornerRadius="4">
                                    <TextBlock Text="{Binding}"/>
                                </Border>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </GroupBox>

                <!-- Questions Section -->
                <GroupBox Header="Application Questions">
                    <ItemsControl ItemsSource="{Binding Questions}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Border Margin="0,8">
                                    <StackPanel>
                                        <TextBlock Text="{Binding QuestionText}"
                                                   FontWeight="SemiBold"/>
                                        <!-- Dynamic input based on Type -->
                                        <ContentControl>
                                            <!-- TextBox for Text/TextArea -->
                                            <!-- ComboBox for Select -->
                                            <!-- RadioButtons for Radio -->
                                        </ContentControl>
                                    </StackPanel>
                                </Border>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </GroupBox>
            </StackPanel>
        </ScrollViewer>

        <!-- Footer: Action buttons -->
        <Border Background="{StaticResource CardBrush}">
            <Grid>
                <TextBlock Text="{Binding StatusMessage}"/>
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                    <Button Content="Cancel" Command="{Binding CancelCommand}"
                            Style="{StaticResource SecondaryButton}"/>
                    <Button Content="Submit Application"
                            Command="{Binding ApproveCommand}"
                            Style="{StaticResource PrimaryButton}"/>
                </StackPanel>
            </Grid>
        </Border>
    </Grid>
</Window>
```

---

## Phase 5: Integration in MainViewModel

### 5.1 Add AutoApply Command

```csharp
[RelayCommand]
private async Task AutoApplyAsync()
{
    if (SelectedJob == null || !SelectedJob.HasEasyApply)
    {
        StatusMessage = "Please select a job with Easy Apply.";
        return;
    }

    if (string.IsNullOrWhiteSpace(_settingsService.Settings.UserProfessionalProfile))
    {
        StatusMessage = "Please configure your professional profile in Settings first.";
        return;
    }

    try
    {
        IsSearching = true;
        var job = await _jobRepository.GetJobByLinkedInIdAsync(SelectedJob.LinkedInJobId);
        if (job == null) return;

        // Step 1: Prepare application (open modal, detect questions)
        StatusMessage = "Preparing application...";
        var session = await _linkedInService.PrepareApplicationAsync(
            job,
            new Progress<string>(s => SearchProgress = s));

        if (session == null || session.Status == ApplicationSessionStatus.Failed)
        {
            StatusMessage = $"Failed to prepare application: {session?.ErrorMessage}";
            return;
        }

        // Step 2: Generate AI message
        StatusMessage = "Generating personalized message...";
        var messageResult = await _kimiService.GenerateApplicationMessageAsync(
            job.Description ?? "",
            job.Title,
            job.Company?.Name ?? "",
            _settingsService.Settings.UserProfessionalProfile);

        session.ApplicationMessage = messageResult?.Message ?? "";

        // Step 3: Generate AI answers for questions
        if (session.Questions.Any())
        {
            StatusMessage = "Generating answers to questions...";
            var answers = await _kimiService.GenerateQuestionAnswersAsync(
                session.Questions,
                _settingsService.Settings.UserProfessionalProfile,
                job.Description ?? "");

            foreach (var q in session.Questions)
            {
                if (answers.TryGetValue(q.QuestionText, out var answer))
                    q.Answer = answer;
            }
        }

        // Step 4: Show review dialog (HUMAN IN THE LOOP)
        IsSearching = false;
        var reviewWindow = new ApplicationReviewWindow(session, messageResult);
        reviewWindow.Owner = Application.Current.MainWindow;
        var approved = reviewWindow.ShowDialog() == true;

        if (!approved)
        {
            StatusMessage = "Application cancelled.";
            await _linkedInService.CancelApplicationAsync();
            return;
        }

        // Step 5: Submit with approved content
        IsSearching = true;
        StatusMessage = "Submitting application...";
        var success = await _linkedInService.SubmitApplicationAsync(
            session,
            new Progress<string>(s => SearchProgress = s));

        if (success)
        {
            // Update database
            await _jobRepository.UpdateJobStatusAsync(job.Id, ApplicationStatus.Applied);
            SelectedJob.Status = ApplicationStatus.Applied;
            SelectedJob.DateApplied = DateTime.Now;

            StatusMessage = $"Application submitted to {job.Company?.Name}!";

            // Log the session
            await LogApplicationSessionAsync(session);
        }
        else
        {
            StatusMessage = $"Submission failed: {session.ErrorMessage}";
        }
    }
    catch (Exception ex)
    {
        StatusMessage = $"Error: {ex.Message}";
    }
    finally
    {
        IsSearching = false;
        SearchProgress = "";
    }
}
```

---

## Phase 6: Logging & Debugging

### 6.1 Application Session Logging

```csharp
private async Task LogApplicationSessionAsync(ApplicationSession session)
{
    var logsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JobFinder", "application-logs");
    Directory.CreateDirectory(logsDir);

    var fileName = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{session.Company}_{session.Status}.json";
    var filePath = Path.Combine(logsDir, fileName);

    var json = JsonSerializer.Serialize(session, new JsonSerializerOptions
    {
        WriteIndented = true
    });

    await File.WriteAllTextAsync(filePath, json);
}
```

### 6.2 Log Contents

Each log file contains:
- Session ID and timestamps
- Job details (title, company, LinkedIn ID)
- The exact message that was submitted
- All questions and the answers provided
- Step-by-step action log with timestamps
- Final status and any error messages

---

## Phase 7: Database Updates

### 7.1 Add ApplicationLog Table (Optional)

For persistent application history:

```csharp
public class ApplicationLog
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public Job Job { get; set; }
    public DateTime AppliedAt { get; set; }
    public string MessageSent { get; set; }
    public string QuestionsJson { get; set; }
    public string ActionsJson { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
```

---

## Implementation Order

### Sprint 1: Foundation (Settings & Models)
1. Update AppSettings.cs with new properties
2. Create new model classes (EasyApplyQuestion, ApplicationSession, ApplicationMessageResult)
3. Update SettingsWindow.xaml with profile section
4. Update SettingsViewModel with new properties

### Sprint 2: AI Integration
5. Extend IKimiService with new methods
6. Implement GenerateApplicationMessageAsync in KimiService
7. Implement GenerateQuestionAnswersAsync in KimiService
8. Test AI message generation

### Sprint 3: LinkedIn Automation
9. Extend ILinkedInService with new methods
10. Implement PrepareApplicationAsync (form detection)
11. Implement question detection logic
12. Implement human-like delay system
13. Implement SubmitApplicationAsync

### Sprint 4: Human-in-the-Loop UI
14. Create ApplicationReviewViewModel
15. Create ApplicationReviewWindow.xaml
16. Wire up approve/cancel flow
17. Add editing capabilities

### Sprint 5: Integration & Polish
18. Add AutoApplyCommand to MainViewModel
19. Add Auto Apply button to MainWindow
20. Implement logging
21. Test complete flow
22. Error handling and edge cases

---

## Risk Mitigation

### LinkedIn Account Safety
- **Rate Limiting**: Maximum 5 applications per hour
- **Session Breaks**: Mandatory 30-minute break after 10 applications
- **Human Patterns**: Variable delays that mimic real user behavior
- **Never Auto-Submit**: Every application requires manual approval

### Error Recovery
- **Modal Stuck**: Implement timeout and close/retry logic
- **Session Expired**: Detect and prompt for re-login
- **Form Changes**: Graceful degradation if form structure changes
- **Network Issues**: Retry with exponential backoff

### Data Safety
- **Profile Data**: Stored locally only, never sent to external services except Kimi API
- **Sensitive Answers**: User reviews all answers before submission
- **Logging**: Full transparency in what was sent

---

## Testing Checklist

- [ ] Profile saves and loads correctly
- [ ] AI generates relevant messages
- [ ] AI answers questions appropriately
- [ ] Easy Apply modal detection works
- [ ] Question types are correctly identified
- [ ] Form filling works for all question types
- [ ] Review dialog displays all information
- [ ] Editing works in review dialog
- [ ] Cancel properly closes modal without submitting
- [ ] Submit correctly fills and submits form
- [ ] Job status updates after successful submission
- [ ] Logging captures all details
- [ ] Error states are handled gracefully
- [ ] Human-like delays are applied
- [ ] Multiple applications work sequentially
