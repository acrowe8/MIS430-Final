using System.Text.Json.Serialization;

namespace api.Models;

public class Course
{
    [JsonPropertyName("course_id")]
    public int CourseId { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}
