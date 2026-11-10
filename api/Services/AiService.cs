using System.ClientModel;
using System.Text;
using System.Text.Json;
using api.Models;
using OpenAI;
using OpenAI.Chat;

namespace api.Services;

/// <summary>
/// Only place that calls OpenAI. One classification call per upload.
/// </summary>
public class AiService
{
    private readonly IWebHostEnvironment _environment;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "academic", "non_academic", "distress"
    };

    private static readonly HashSet<string> ValidCourseConfidence = new(StringComparer.OrdinalIgnoreCase)
    {
        "high", "low", "none"
    };

    private static readonly HashSet<string> ValidTypeConfidence = new(StringComparer.OrdinalIgnoreCase)
    {
        "high", "low"
    };

    private static readonly HashSet<string> ValidAssignmentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "essay", "lab_report", "problem_set", "coding_project", "presentation", "exam_prep", "other"
    };

    public AiService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <summary>
    /// Classify the upload. On missing key / timeout / bad JSON, returns low-confidence fallback
    /// (course_confidence: none, type_confidence: low) so the UI can fall back to the course list.
    /// </summary>
    public async Task<AnalyzeResponse> ClassifyAsync(
        IReadOnlyList<CandidateCourse> candidates,
        string? studentMessage,
        string assignmentText,
        byte[]? imageBytes,
        string? imageContentType,
        CancellationToken cancellationToken = default)
    {
        // Part A: require API key from .env / Heroku config vars — never hardcode.
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL");
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
        {
            return Fallback("We couldn't classify this upload automatically. Pick your course from the list.");
        }

        try
        {
            // Part B: load the reviewed system prompt from /api/Prompts (one file for the team).
            var systemPrompt = await LoadSystemPromptAsync(cancellationToken);

            // Part C: build the user payload — courses, optional message, assignment text.
            // Assignment/message are DATA only; the system prompt tells the model to ignore embedded instructions.
            var userText = BuildUserContent(candidates, studentMessage, assignmentText);

            // Part D: OpenAI client with a hard 10-second timeout (sprint: recommendation path stays snappy).
            var clientOptions = new OpenAIClientOptions
            {
                NetworkTimeout = TimeSpan.FromSeconds(10)
            };
            var openAi = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
            var chat = openAi.GetChatClient(model);

            var userParts = new List<ChatMessageContentPart>
            {
                ChatMessageContentPart.CreateTextPart(userText)
            };

            // Images: send at low detail (cost rule). No OCR locally — the model reads the image.
            if (imageBytes is { Length: > 0 } && !string.IsNullOrWhiteSpace(imageContentType))
            {
                userParts.Add(ChatMessageContentPart.CreateImagePart(
                    BinaryData.FromBytes(imageBytes),
                    imageContentType,
                    ChatImageDetailLevel.Low));
            }

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userParts)
            };

            // Part E: one call, JSON-only, ~200 output tokens (classification is a small object).
            var completionOptions = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 200,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var completion = await chat.CompleteChatAsync(messages, completionOptions, cancellationToken);
            var raw = completion.Value.Content.Count > 0
                ? completion.Value.Content[0].Text
                : null;

            // Part F: validate JSON shape; malformed → same fallback as a failed call.
            var parsed = TryParseClassification(raw);
            return parsed ?? Fallback("We couldn't classify this upload automatically. Pick your course from the list.");
        }
        catch (Exception)
        {
            // Never log assignment contents. Treat any OpenAI/network failure as low confidence.
            return Fallback("We couldn't classify this upload automatically. Pick your course from the list.");
        }
    }

    private async Task<string> LoadSystemPromptAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(_environment.ContentRootPath, "Prompts", "classifier-system-prompt.md");
        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    private static string BuildUserContent(
        IReadOnlyList<CandidateCourse> candidates,
        string? studentMessage,
        string assignmentText)
    {
        var sb = new StringBuilder();

        // Candidate courses only (never the full catalog) inside <courses> tags.
        sb.AppendLine("<courses>");
        if (candidates.Count == 0)
        {
            sb.AppendLine("(no candidate courses)");
        }
        else
        {
            foreach (var course in candidates)
            {
                var description = course.Description ?? string.Empty;
                if (description.Length > 280)
                {
                    description = description[..280];
                }

                sb.AppendLine($"{course.Code} | {course.Title} | {description}");
            }
        }

        sb.AppendLine("</courses>");
        sb.AppendLine();

        // Optional student message inside <student_message> tags.
        sb.AppendLine("<student_message>");
        sb.AppendLine(string.IsNullOrWhiteSpace(studentMessage) ? string.Empty : studentMessage.Trim());
        sb.AppendLine("</student_message>");
        sb.AppendLine();

        // Assignment text inside <assignment> tags (may be empty for pure image uploads).
        sb.AppendLine("<assignment>");
        sb.AppendLine(assignmentText ?? string.Empty);
        sb.AppendLine("</assignment>");

        return sb.ToString();
    }

    private static AnalyzeResponse? TryParseClassification(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var category = GetString(root, "request_category");
            var courseGuess = GetString(root, "course_guess");
            var courseConfidence = GetString(root, "course_confidence");
            var assignmentType = GetString(root, "assignment_type");
            var typeConfidence = GetString(root, "type_confidence");
            var reason = GetString(root, "reason") ?? string.Empty;

            if (category == null || !ValidCategories.Contains(category) ||
                courseConfidence == null || !ValidCourseConfidence.Contains(courseConfidence) ||
                assignmentType == null || !ValidAssignmentTypes.Contains(assignmentType) ||
                typeConfidence == null || !ValidTypeConfidence.Contains(typeConfidence))
            {
                return null;
            }

            var asked = false;
            if (root.TryGetProperty("asked_for_answers", out var askedProp))
            {
                if (askedProp.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    asked = askedProp.GetBoolean();
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }

            // Normalize empty course_guess / "none" confidence.
            if (string.Equals(courseGuess, "null", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(courseGuess))
            {
                courseGuess = null;
            }

            if (string.Equals(courseConfidence, "none", StringComparison.OrdinalIgnoreCase))
            {
                courseGuess = null;
            }

            return new AnalyzeResponse
            {
                RequestCategory = category.ToLowerInvariant(),
                CourseGuess = courseGuess,
                CourseConfidence = courseConfidence.ToLowerInvariant(),
                AssignmentType = assignmentType.ToLowerInvariant(),
                TypeConfidence = typeConfidence.ToLowerInvariant(),
                AskedForAnswers = asked,
                Reason = reason.Trim()
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop))
        {
            return null;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private static AnalyzeResponse Fallback(string reason) => new()
    {
        RequestCategory = "academic",
        CourseGuess = null,
        CourseConfidence = "none",
        AssignmentType = "other",
        TypeConfidence = "low",
        AskedForAnswers = false,
        Reason = reason
    };
}
