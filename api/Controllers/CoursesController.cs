using api.Services;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("api/courses")]
public class CoursesController : ControllerBase
{
    private readonly CourseService _courseService;

    public CoursesController(CourseService courseService)
    {
        _courseService = courseService;
    }

    /// <summary>Searchable course list for the Yes/No fallback picker.</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var courses = await _courseService.SearchAsync(search, cancellationToken);
        return Ok(courses);
    }
}
