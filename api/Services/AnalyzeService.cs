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

        // File names often encode the class (e.g. ..._GBA_146_...); treat _ and - as spaces for search.
        var fileNameForSearch = NormalizeFileNameSeparators(upload.FileName);

        // 2) Text used for DB narrowing: file name first (header bias), then assignment text + message.
        //    (Images have no extracted text; file name / message still help find a course code.)
        var textForCandidates = string.Join(
            '\n',
            new[] { fileNameForSearch, upload.ExtractedText, message }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        // 3) CourseService picks ≤15 candidates (code/name hits first, else FULLTEXT).
        var candidates = await _courses.FindCandidatesAsync(textForCandidates, cancellationToken);

        // 4) Exactly one OpenAI call — AiService owns the prompt, timeout, and JSON validation.
        var classification = await _ai.ClassifyAsync(
            candidates,
            message,
            upload.ExtractedText,
            upload.FileName,
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

            // Don't trust AI "high" unless the course is actually named in the file text or file name.
            if (string.Equals(classification.CourseConfidence, "high", StringComparison.OrdinalIgnoreCase))
            {
                var namingEvidence = string.Join(
                    '\n',
                    new[] { upload.ExtractedText, upload.FileName, fileNameForSearch }
                        .Where(s => !string.IsNullOrWhiteSpace(s)));

                var named = await _courses.IsCourseNamedInTextAsync(
                    matched.Code, namingEvidence, cancellationToken);
                if (!named)
                {
                    classification.CourseConfidence = "low";
                }
            }
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

    /// <summary>Students often put course codes in file names with underscores/dashes as separators.</summary>
    private static string NormalizeFileNameSeparators(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        return fileName.Replace('_', ' ').Replace('-', ' ');
    }
}
