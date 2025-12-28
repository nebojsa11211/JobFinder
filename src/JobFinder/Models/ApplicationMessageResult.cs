namespace JobFinder.Models;

/// <summary>
/// Result from AI-generated application message.
/// </summary>
public class ApplicationMessageResult
{
    /// <summary>
    /// The personalized cover letter/message to send with the application.
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Key requirements from the job description that were addressed in the message.
    /// </summary>
    public List<string> AddressedRequirements { get; set; } = [];

    /// <summary>
    /// Skills from the user's profile that match the job requirements.
    /// </summary>
    public List<string> MatchingSkills { get; set; } = [];

    /// <summary>
    /// AI confidence score (0-100) indicating how well the message matches the job.
    /// Higher scores indicate better alignment between profile and job requirements.
    /// </summary>
    public int ConfidenceScore { get; set; }

    /// <summary>
    /// Whether parsing the AI response failed.
    /// If true, the message may be a fallback or template-based.
    /// </summary>
    public bool ParseFailed { get; set; }

    /// <summary>
    /// Raw AI response for debugging purposes.
    /// </summary>
    public string? RawResponse { get; set; }

    /// <summary>
    /// The prompt that was sent to the AI.
    /// </summary>
    public string? PromptSent { get; set; }

    /// <summary>
    /// Error message if generation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result from AI-generated question answers.
/// </summary>
public class QuestionAnswersResult
{
    /// <summary>
    /// Whether the generation succeeded.
    /// </summary>
    public bool Success => string.IsNullOrEmpty(ErrorMessage) && !ParseFailed;

    /// <summary>
    /// Dictionary mapping question text to answer.
    /// </summary>
    public Dictionary<string, string> Answers { get; set; } = new();

    /// <summary>
    /// Whether parsing the AI response failed.
    /// </summary>
    public bool ParseFailed { get; set; }

    /// <summary>
    /// Raw AI response for debugging.
    /// </summary>
    public string? RawResponse { get; set; }

    /// <summary>
    /// Error message if generation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
