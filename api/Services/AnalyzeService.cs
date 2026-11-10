using api.Models;

namespace api.Services;

/// <summary>
/// Orchestrates upload → candidate courses → one OpenAI classification → matched course fields.
/// </summary>
public class AnalyzeService
{
    private readonly FileExtractionService _files;
    private readonly CourseService _courses;
    private readonly AiService _ai;

    public AnalyzeService(FileExtractionService files, CourseService courses, AiService ai)
    {
        _files = files;
        _courses = courses;
        _ai = ai;
    }

    public async Task<AnalyzeResponse> AnalyzeAsync(
        IFormFile file,
        string? message,
        CancellationToken cancellationToken = default)
    {
        // 1) Validate + read file in memory; extract PDF/docx text (first 3,000 chars).
        var upload = await _files.ReadAsync(file, cancellationToken);

        // 2) Text used for DB narrowing: extracted assignment text + optional student message.
        //    (Images have no extracted text; message still helps find a course code.)
        var textForCandidates = string.Join(
            '\n',
            new[] { upload.ExtractedText, message }.Where(s => !string.IsNullOrWhiteSpace(s)));

        // 3) CourseService picks ≤15 candidates (code/name hits first, else FULLTEXT).
        var candidates = await _courses.FindCandidatesAsync(textForCandidates, cancellationToken);

        // 4) Exactly one OpenAI call — AiService owns the prompt, timeout, and JSON validation.
        var classification = await _ai.ClassifyAsync(
            candidates,
            message,
            upload.ExtractedText,
            upload.IsImage ? upload.Bytes : null,
            upload.IsImage ? upload.ContentType : null,
            cancellationToken);

        // 5) If the AI guessed a real course, attach course_id / code / title from our DB.
        var matched = await _courses.FindByCodeAsync(classification.CourseGuess, cancellationToken);
        if (matched != null)
        {
            classification.CourseId = matched.CourseId;
            classification.Code = matched.Code;
            classification.Title = matched.Title;
            // Prefer the canonical DB code spelling.
            classification.CourseGuess = matched.Code;
        }
        else if (!string.Equals(classification.CourseConfidence, "none", StringComparison.OrdinalIgnoreCase))
        {
            // Guess wasn't in our data — treat as no course match for the Yes/No UI.
            classification.CourseGuess = null;
            classification.CourseConfidence = "none";
            classification.CourseId = null;
            classification.Code = null;
            classification.Title = null;
        }

        return classification;
    }
}
