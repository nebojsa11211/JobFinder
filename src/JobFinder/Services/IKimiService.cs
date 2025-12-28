using JobFinder.Models;

namespace JobFinder.Services;

public interface IKimiService
{
    /// <summary>
    /// Analyzes a job description and returns a summary with rating.
    /// </summary>
    Task<JobSummaryResult?> GetSummaryAsync(string jobDescription, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a personalized application message based on job and user profile.
    /// </summary>
    /// <param name="jobDescription">The full job description text.</param>
    /// <param name="jobTitle">The job title.</param>
    /// <param name="company">The company name.</param>
    /// <param name="userProfile">The user's professional profile/CV content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated application message with metadata.</returns>
    Task<ApplicationMessageResult?> GenerateApplicationMessageAsync(
        string jobDescription,
        string jobTitle,
        string company,
        string userProfile,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates answers for Easy Apply questions based on user profile.
    /// </summary>
    /// <param name="questions">List of questions to answer.</param>
    /// <param name="userProfile">The user's professional profile/CV content.</param>
    /// <param name="jobDescription">The job description for context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with answers mapped to question text.</returns>
    Task<QuestionAnswersResult> GenerateQuestionAnswersAsync(
        List<EasyApplyQuestion> questions,
        string userProfile,
        string jobDescription,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the API key by making a test request.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> ValidateApiKeyAsync();

    /// <summary>
    /// Whether the API key is configured.
    /// </summary>
    bool IsConfigured { get; }
}
