using api.Models;
using api.Services;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("api/recommend")]
public class RecommendController : ControllerBase
{
    private readonly RecommendationService _recommendationService;

    public RecommendController(RecommendationService recommendationService)
    {
        _recommendationService = recommendationService;
    }

    /// <summary>
    /// Pick one UA resource for the confirmed course + assignment type.
    /// Distress / non-academic requests get a handoff message instead.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Recommend(
        [FromBody] RecommendRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        try
        {
            var result = await _recommendationService.RecommendAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
