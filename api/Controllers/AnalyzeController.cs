using api.Services;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("api/analyze")]
public class AnalyzeController : ControllerBase
{
    private readonly AnalyzeService _analyzeService;

    public AnalyzeController(AnalyzeService analyzeService)
    {
        _analyzeService = analyzeService;
    }

    /// <summary>
    /// Upload an assignment (PDF / .docx / image) plus optional message.
    /// Returns AI classification only — does not save the file.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(FileExtractionService.MaxFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileExtractionService.MaxFileBytes)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Analyze(
        IFormFile file,
        [FromForm] string? message,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _analyzeService.AnalyzeAsync(file, message, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            // Clear student-facing errors (bad type, too large, missing file).
            return BadRequest(new { error = ex.Message });
        }
    }
}
