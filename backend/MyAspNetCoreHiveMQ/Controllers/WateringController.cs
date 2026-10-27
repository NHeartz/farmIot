using Microsoft.AspNetCore.Mvc;
using MyAspNetCoreHiveMQ.Models;
using MyAspNetCoreHiveMQ.Services;

namespace MyAspNetCoreHiveMQ.Controllers;

[ApiController]
[Route("api/watering")]
public sealed class WateringController : ControllerBase
{
    private readonly IWateringService _wateringService;

    public WateringController(IWateringService wateringService)
    {
        _wateringService = wateringService;
    }

    [HttpPost("manual")]
    public async Task<IActionResult> StartManual([FromBody] CreateWateringRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _wateringService.StartManualAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("schedule")]
    public async Task<IActionResult> Schedule([FromBody] ScheduleWateringRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _wateringService.ScheduleAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("plots/{plotName}/stop")]
    public async Task<IActionResult> StopPlot(string plotName, CancellationToken cancellationToken)
    {
        var result = await _wateringService.StopPlotAsync(plotName, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("plots/{plotName}/auto")]
    public async Task<IActionResult> SetAutomaticPolicy(string plotName,
        [FromBody] AutomaticPolicyRequest request, CancellationToken cancellationToken)
    {
        var result = await _wateringService.SetAutomaticPolicyAsync(plotName, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> GetJobs([FromQuery] string? plotName,
        [FromQuery] int limit = 100, CancellationToken cancellationToken = default)
    {
        var result = await _wateringService.GetJobsAsync(plotName, limit, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("jobs/{id:long}")]
    public async Task<IActionResult> GetJob(long id, CancellationToken cancellationToken)
    {
        var result = await _wateringService.GetJobAsync(id, cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(WateringServiceResult result)
    {
        if (result.StatusCode == StatusCodes.Status202Accepted && result.ResourceId is not null)
            return AcceptedAtAction(nameof(GetJob), new { id = result.ResourceId }, result.Value);

        return result.StatusCode switch
        {
            StatusCodes.Status200OK => Ok(result.Value),
            StatusCodes.Status202Accepted => Accepted(result.Value),
            StatusCodes.Status400BadRequest => BadRequest(new { message = result.Message }),
            StatusCodes.Status404NotFound when result.Message is null => NotFound(),
            StatusCodes.Status404NotFound => NotFound(new { message = result.Message }),
            StatusCodes.Status503ServiceUnavailable => StatusCode(result.StatusCode,
                new { message = result.Message }),
            _ => StatusCode(result.StatusCode, result.Value)
        };
    }
}
