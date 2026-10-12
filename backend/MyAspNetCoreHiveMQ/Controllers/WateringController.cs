using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyAspNetCoreHiveMQ.Data;
using MyAspNetCoreHiveMQ.Models;
using MyAspNetCoreHiveMQ.Services;

namespace MyAspNetCoreHiveMQ.Controllers;

[ApiController]
[Route("api/watering")]
public sealed class WateringController : ControllerBase
{
    private static readonly Regex PlotNamePattern = new("^[a-zA-Z0-9_-]{1,50}$", RegexOptions.Compiled);
    private readonly ApplicationDbContext _db;
    private readonly IMqttCommandPublisher _publisher;

    public WateringController(ApplicationDbContext db, IMqttCommandPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    [HttpPost("manual")]
    public async Task<IActionResult> StartManual([FromBody] CreateWateringRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidPlot(request.PlotName))
            return BadRequest(new { message = "plotName may contain only letters, numbers, _ and -" });

        var job = CreateJob(request.PlotName, request.DurationSeconds, "manual", priority: 10,
            DateTime.UtcNow);
        _db.WateringJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
        return AcceptedAtAction(nameof(GetJob), new { id = job.Id }, job);
    }

    [HttpPost("schedule")]
    public async Task<IActionResult> Schedule([FromBody] ScheduleWateringRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidPlot(request.PlotName))
            return BadRequest(new { message = "plotName may contain only letters, numbers, _ and -" });

        var scheduledAt = request.ScheduledAt.UtcDateTime;
        if (scheduledAt < DateTime.UtcNow.AddSeconds(-5))
            return BadRequest(new { message = "scheduledAt must be in the future" });

        var job = CreateJob(request.PlotName, request.DurationSeconds, "scheduled", priority: 5,
            scheduledAt);
        _db.WateringJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
        return AcceptedAtAction(nameof(GetJob), new { id = job.Id }, job);
    }

    [HttpPost("plots/{plotName}/stop")]
    public async Task<IActionResult> StopPlot(string plotName, CancellationToken cancellationToken)
    {
        if (!ValidPlot(plotName))
            return BadRequest(new { message = "Invalid plotName" });

        var job = await _db.WateringJobs
            .Where(item => item.PlotName == plotName &&
                           (item.Status == "pending" || item.Status == "processing"))
            .OrderByDescending(item => item.Status == "processing")
            .ThenBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
            return NotFound(new { message = "No pending or active watering job for this plot" });

        if (job.Status == "processing")
        {
            var published = await _publisher.PublishWateringCommandAsync(
                job.PlotName, job.Id, "stop", 0, cancellationToken);
            if (!published)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "MQTT is disconnected; job remains processing until timeout" });

            return Accepted(new { job.Id, status = "processing", message = "Stop command sent" });
        }

        job.Status = "cancelled";
        job.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(job);
    }

    [HttpPut("plots/{plotName}/auto")]
    public async Task<IActionResult> SetAutomaticPolicy(string plotName,
        [FromBody] AutomaticPolicyRequest request, CancellationToken cancellationToken)
    {
        if (!ValidPlot(plotName))
            return BadRequest(new { message = "Invalid plotName" });

        var policy = await _db.WateringPolicies.FindAsync([plotName], cancellationToken);
        if (policy is null)
        {
            policy = new WateringPolicy { PlotName = plotName };
            _db.WateringPolicies.Add(policy);
        }

        policy.AutoEnabled = request.Enabled;
        policy.SoilThresholdPercent = request.SoilThresholdPercent;
        policy.DurationSeconds = request.DurationSeconds;
        policy.CooldownMinutes = request.CooldownMinutes;
        policy.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(policy);
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> GetJobs([FromQuery] string? plotName,
        [FromQuery] int limit = 100, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var query = _db.WateringJobs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(plotName))
        {
            if (!ValidPlot(plotName))
                return BadRequest(new { message = "Invalid plotName" });
            query = query.Where(job => job.PlotName == plotName);
        }

        var jobs = await query.OrderByDescending(job => job.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return Ok(jobs);
    }

    [HttpGet("jobs/{id:long}")]
    public async Task<IActionResult> GetJob(long id, CancellationToken cancellationToken)
    {
        var job = await _db.WateringJobs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }

    private static bool ValidPlot(string plotName) =>
        !string.IsNullOrWhiteSpace(plotName) && PlotNamePattern.IsMatch(plotName);

    private static WateringJob CreateJob(string plotName, int durationSeconds, string mode,
        short priority, DateTime scheduledAt) => new()
    {
        PlotName = plotName,
        DurationSeconds = durationSeconds,
        Mode = mode,
        Status = "pending",
        Priority = priority,
        ScheduledAt = scheduledAt,
        CreatedAt = DateTime.UtcNow
    };
}

public sealed record CreateWateringRequest(
    [param: Required, StringLength(50)] string PlotName,
    [param: Range(1, 86400)] int DurationSeconds);

public sealed record ScheduleWateringRequest(
    [param: Required, StringLength(50)] string PlotName,
    [param: Range(1, 86400)] int DurationSeconds,
    DateTimeOffset ScheduledAt);

public sealed record AutomaticPolicyRequest(
    bool Enabled,
    [param: Range(typeof(decimal), "0", "100")] decimal SoilThresholdPercent,
    [param: Range(1, 86400)] int DurationSeconds,
    [param: Range(0, 10080)] int CooldownMinutes);
