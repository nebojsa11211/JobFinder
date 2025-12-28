namespace JobFinder.Models;

/// <summary>
/// Represents a question/field in a LinkedIn Easy Apply form.
/// </summary>
public class EasyApplyQuestion
{
    /// <summary>
    /// Unique identifier for this question instance.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The question text as displayed in the form.
    /// </summary>
    public string QuestionText { get; set; } = "";

    /// <summary>
    /// Type of input field.
    /// </summary>
    public QuestionType Type { get; set; } = QuestionType.Text;

    /// <summary>
    /// Available options for Select/Radio/Checkbox questions.
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
    /// For multi-select questions, the selected options.
    /// </summary>
    public List<string> SelectedOptions { get; set; } = [];

    /// <summary>
    /// Playwright selector for the input element.
    /// </summary>
    public string Selector { get; set; } = "";

    /// <summary>
    /// The page number in the Easy Apply flow (0-indexed).
    /// </summary>
    public int PageIndex { get; set; }

    /// <summary>
    /// Label/placeholder text if different from question text.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Maximum length for text inputs if specified.
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Whether this field was pre-filled by LinkedIn.
    /// </summary>
    public bool IsPreFilled { get; set; }

    /// <summary>
    /// The pre-filled value if any.
    /// </summary>
    public string? PreFilledValue { get; set; }
}

/// <summary>
/// Types of input fields in Easy Apply forms.
/// </summary>
public enum QuestionType
{
    /// <summary>
    /// Single-line text input.
    /// </summary>
    Text,

    /// <summary>
    /// Multi-line text area.
    /// </summary>
    TextArea,

    /// <summary>
    /// Dropdown select box.
    /// </summary>
    Select,

    /// <summary>
    /// Radio button group (single selection).
    /// </summary>
    Radio,

    /// <summary>
    /// Checkbox (boolean or multiple selection).
    /// </summary>
    Checkbox,

    /// <summary>
    /// Numeric input.
    /// </summary>
    Number,

    /// <summary>
    /// Yes/No question (often radio buttons).
    /// </summary>
    YesNo,

    /// <summary>
    /// Phone number input.
    /// </summary>
    Phone,

    /// <summary>
    /// Email input.
    /// </summary>
    Email,

    /// <summary>
    /// Date picker.
    /// </summary>
    Date,

    /// <summary>
    /// File upload (resume, cover letter).
    /// </summary>
    FileUpload,

    /// <summary>
    /// Unknown or unsupported field type.
    /// </summary>
    Unknown
}
