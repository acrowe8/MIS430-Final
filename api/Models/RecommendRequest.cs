using System.Text.Json.Serialization;

namespace api.Models;

/// <summary>POST /api/recommend body: course + assignment context from analyze / follow-up.</summary>
public class RecommendRequest
{
    [JsonPropertyName("course_id")]
    public int? CourseId { get; set; }

    [JsonPropertyName("assignment_type")]
    public string? AssignmentType { get; set; }

    [JsonPropertyName("request_category")]
    public string? RequestCategory { get; set; }

    [JsonPropertyName("asked_for_answers")]
    public bool AskedForAnswers { get; set; }
}
