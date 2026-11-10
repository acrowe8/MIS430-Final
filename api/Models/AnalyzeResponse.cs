using System.Text.Json.Serialization;

namespace api.Models;

/// <summary>POST /api/analyze response: AI classification + matched course fields when known.</summary>
public class AnalyzeResponse
{
    [JsonPropertyName("request_category")]
    public string RequestCategory { get; set; } = "academic";

    [JsonPropertyName("course_guess")]
    public string? CourseGuess { get; set; }

    [JsonPropertyName("course_confidence")]
    public string CourseConfidence { get; set; } = "none";

    [JsonPropertyName("assignment_type")]
    public string AssignmentType { get; set; } = "other";

    [JsonPropertyName("type_confidence")]
    public string TypeConfidence { get; set; } = "low";

    [JsonPropertyName("asked_for_answers")]
    public bool AskedForAnswers { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    // Matched course from our DB when course_guess lines up with a real course.
    [JsonPropertyName("course_id")]
    public int? CourseId { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}
