namespace api.Models;

/// <summary>Course row sent to the AI (includes description for matching).</summary>
public class CandidateCourse
{
    public int CourseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
