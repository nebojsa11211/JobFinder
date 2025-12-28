using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobFinder.Models;
using System.Collections.ObjectModel;

namespace JobFinder.ViewModels;

/// <summary>
/// ViewModel for the Application Review dialog.
/// Allows users to review and edit AI-generated answers before submission.
/// </summary>
public partial class ApplicationReviewViewModel : ObservableObject
{
    /// <summary>
    /// The application session being reviewed.
    /// </summary>
    public ApplicationSession Session { get; }

    /// <summary>
    /// Job title for display.
    /// </summary>
    public string JobTitle => Session.JobTitle;

    /// <summary>
    /// Company name for display.
    /// </summary>
    public string Company => Session.Company;

    /// <summary>
    /// Confidence score from AI analysis.
    /// </summary>
    public int ConfidenceScore => Session.ConfidenceScore;

    /// <summary>
    /// Confidence level description based on score.
    /// </summary>
    public string ConfidenceLevel => ConfidenceScore switch
    {
        >= 80 => "High",
        >= 60 => "Medium",
        _ => "Low"
    };

    /// <summary>
    /// Color indicator for confidence level.
    /// </summary>
    public string ConfidenceColor => ConfidenceScore switch
    {
        >= 80 => "#4CAF50",
        >= 60 => "#FF9800",
        _ => "#F44336"
    };

    /// <summary>
    /// The editable application message.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MessageCharacterCount))]
    private string _applicationMessage;

    /// <summary>
    /// Character count for the application message.
    /// </summary>
    public int MessageCharacterCount => ApplicationMessage?.Length ?? 0;

    /// <summary>
    /// Observable collection of questions for binding.
    /// </summary>
    public ObservableCollection<QuestionViewModel> Questions { get; } = [];

    /// <summary>
    /// Skills from profile that match the job.
    /// </summary>
    public ObservableCollection<string> MatchingSkills { get; } = [];

    /// <summary>
    /// Requirements addressed in the application.
    /// </summary>
    public ObservableCollection<string> AddressedRequirements { get; } = [];

    /// <summary>
    /// Number of questions that need answers.
    /// </summary>
    public int QuestionCount => Questions.Count;

    /// <summary>
    /// Whether there are questions to display.
    /// </summary>
    public bool HasQuestions => Questions.Count > 0;

    /// <summary>
    /// Whether there are matching skills to display.
    /// </summary>
    public bool HasMatchingSkills => MatchingSkills.Count > 0;

    /// <summary>
    /// Result of the dialog: true = approved, false = cancelled.
    /// </summary>
    public bool? DialogResult { get; private set; }

    /// <summary>
    /// Event raised when the dialog should close.
    /// </summary>
    public event EventHandler<bool>? RequestClose;

    public ApplicationReviewViewModel(ApplicationSession session)
    {
        Session = session;
        _applicationMessage = session.ApplicationMessage;

        // Populate questions
        foreach (var question in session.Questions.Where(q => !q.IsPreFilled))
        {
            Questions.Add(new QuestionViewModel(question));
        }

        // Populate matching skills
        foreach (var skill in session.MatchingSkills)
        {
            MatchingSkills.Add(skill);
        }

        // Populate addressed requirements
        foreach (var req in session.AddressedRequirements)
        {
            AddressedRequirements.Add(req);
        }
    }

    /// <summary>
    /// Approves the application and closes the dialog.
    /// </summary>
    [RelayCommand]
    private void Approve()
    {
        // Update session with edited values
        Session.ApplicationMessage = ApplicationMessage;
        Session.Status = ApplicationSessionStatus.Approved;

        // Update questions with edited answers
        foreach (var qvm in Questions)
        {
            qvm.UpdateSource();
        }

        DialogResult = true;
        RequestClose?.Invoke(this, true);
    }

    /// <summary>
    /// Cancels the application and closes the dialog.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        Session.Status = ApplicationSessionStatus.Cancelled;
        DialogResult = false;
        RequestClose?.Invoke(this, false);
    }
}

/// <summary>
/// ViewModel for individual questions in the review dialog.
/// </summary>
public partial class QuestionViewModel : ObservableObject
{
    private readonly EasyApplyQuestion _question;

    /// <summary>
    /// The question text.
    /// </summary>
    public string QuestionText => _question.QuestionText;

    /// <summary>
    /// The question type for display.
    /// </summary>
    public string TypeDisplay => _question.Type.ToString();

    /// <summary>
    /// Whether this is a required question.
    /// </summary>
    public bool IsRequired => _question.IsRequired;

    /// <summary>
    /// The editable answer.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnswerCharacterCount))]
    private string _answer;

    /// <summary>
    /// Character count for the answer.
    /// </summary>
    public int AnswerCharacterCount => Answer?.Length ?? 0;

    /// <summary>
    /// Maximum length if specified.
    /// </summary>
    public int? MaxLength => _question.MaxLength;

    /// <summary>
    /// Whether max length is specified.
    /// </summary>
    public bool HasMaxLength => MaxLength.HasValue;

    /// <summary>
    /// Available options for select/radio questions.
    /// </summary>
    public ObservableCollection<string> Options { get; } = [];

    /// <summary>
    /// Whether this is a multi-line question (TextArea).
    /// </summary>
    public bool IsMultiLine => _question.Type == QuestionType.TextArea;

    /// <summary>
    /// Whether this is a select question.
    /// </summary>
    public bool IsSelect => _question.Type == QuestionType.Select || _question.Type == QuestionType.Radio;

    /// <summary>
    /// Whether this is a simple text input.
    /// </summary>
    public bool IsTextInput => _question.Type is QuestionType.Text or QuestionType.Number
        or QuestionType.Phone or QuestionType.Email or QuestionType.Date;

    /// <summary>
    /// Page number in the Easy Apply flow.
    /// </summary>
    public int PageNumber => _question.PageIndex + 1;

    public QuestionViewModel(EasyApplyQuestion question)
    {
        _question = question;
        _answer = question.Answer;

        foreach (var option in question.Options)
        {
            Options.Add(option);
        }
    }

    /// <summary>
    /// Updates the source question with the edited answer.
    /// </summary>
    public void UpdateSource()
    {
        _question.Answer = Answer;
    }
}
