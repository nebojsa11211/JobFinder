namespace JobFinder.Models;

/// <summary>
/// Represents a complete job application session, including all questions,
/// answers, and actions taken. Used for human review and logging.
/// </summary>
public class ApplicationSession
{
    /// <summary>
    /// Unique identifier for this application session.
    /// </summary>
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Database ID of the job being applied to.
    /// </summary>
    public int JobId { get; set; }

    /// <summary>
    /// The platform this application is for.
    /// </summary>
    public JobPlatform Platform { get; set; } = JobPlatform.LinkedIn;

    /// <summary>
    /// External job ID from the platform (LinkedIn job ID, Upwork job ID, etc.).
    /// </summary>
    public string ExternalJobId { get; set; } = "";

    /// <summary>
    /// LinkedIn job ID (alias for ExternalJobId for backward compatibility).
    /// </summary>
    [Obsolete("Use ExternalJobId instead")]
    public string LinkedInJobId
    {
        get => ExternalJobId;
        set => ExternalJobId = value;
    }

    /// <summary>
    /// Job title for display and logging.
    /// </summary>
    public string JobTitle { get; set; } = "";

    /// <summary>
    /// Company name for display and logging.
    /// </summary>
    public string Company { get; set; } = "";

    /// <summary>
    /// Job URL.
    /// </summary>
    public string JobUrl { get; set; } = "";

    /// <summary>
    /// When the application session started.
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// When the application was completed (submitted or cancelled).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// The personalized message to be submitted with the application.
    /// </summary>
    public string ApplicationMessage { get; set; } = "";

    /// <summary>
    /// All questions detected in the Easy Apply form.
    /// </summary>
    public List<EasyApplyQuestion> Questions { get; set; } = [];

    /// <summary>
    /// Step-by-step log of all actions taken during the session.
    /// </summary>
    public List<ApplicationAction> Actions { get; set; } = [];

    /// <summary>
    /// Current status of the application session.
    /// </summary>
    public ApplicationSessionStatus Status { get; set; } = ApplicationSessionStatus.Pending;

    /// <summary>
    /// Error message if the application failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of pages in the Easy Apply form.
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Current page being processed (0-indexed).
    /// </summary>
    public int CurrentPage { get; set; }

    /// <summary>
    /// Skills from profile that match the job (from AI analysis).
    /// </summary>
    public List<string> MatchingSkills { get; set; } = [];

    /// <summary>
    /// Requirements addressed in the application message.
    /// </summary>
    public List<string> AddressedRequirements { get; set; } = [];

    /// <summary>
    /// AI confidence score for the application message (0-100).
    /// </summary>
    public int ConfidenceScore { get; set; }

    /// <summary>
    /// Adds an action to the session log.
    /// </summary>
    public void LogAction(string actionType, string description, bool success = true, string? details = null)
    {
        Actions.Add(new ApplicationAction
        {
            ActionType = actionType,
            Description = description,
            Success = success,
            Details = details
        });
    }
}

/// <summary>
/// Represents a single action taken during an application session.
/// </summary>
public class ApplicationAction
{
    /// <summary>
    /// Timestamp when the action occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Type of action (e.g., "Navigate", "Click", "Fill", "Submit").
    /// </summary>
    public string ActionType { get; set; } = "";

    /// <summary>
    /// Human-readable description of the action.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Whether the action succeeded.
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Additional details (e.g., the value entered, error message).
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Duration of the action in milliseconds.
    /// </summary>
    public int? DurationMs { get; set; }
}

/// <summary>
/// Status of an application session.
/// </summary>
public enum ApplicationSessionStatus
{
    /// <summary>
    /// Application is being prepared (navigating, detecting form).
    /// </summary>
    Pending,

    /// <summary>
    /// Form analyzed, waiting for human review and approval.
    /// </summary>
    ReadyForReview,

    /// <summary>
    /// User approved the application, ready to submit.
    /// </summary>
    Approved,

    /// <summary>
    /// Currently filling and submitting the form.
    /// </summary>
    Submitting,

    /// <summary>
    /// Application successfully submitted.
    /// </summary>
    Submitted,

    /// <summary>
    /// Application failed due to an error.
    /// </summary>
    Failed,

    /// <summary>
    /// User cancelled the application.
    /// </summary>
    Cancelled
}
