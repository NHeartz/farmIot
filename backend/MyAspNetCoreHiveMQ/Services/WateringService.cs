using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MyAspNetCoreHiveMQ.Data;
using MyAspNetCoreHiveMQ.Models;

namespace MyAspNetCoreHiveMQ.Services;

public sealed class WateringService : IWateringService
{
    private static readonly Regex PlotNamePattern = new("^[a-zA-Z0-9_-]{1,50}$", RegexOptions.Compiled);
    private readonly ApplicationDbContext _db;
    private readonly IMqttCommandPublisher _publisher;

    public WateringService(ApplicationDbContext db, IMqttCommandPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public async Task<WateringServiceResult> StartManualAsync(CreateWateringRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidPlot(request.PlotName))
            return BadRequest("plotName may contain only letters, numbers, _ and -");

        var job = CreateJob(request.PlotName, request.DurationSeconds, "manual", priority: 10,
            DateTime.UtcNow);
        _db.WateringJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
        return new WateringServiceResult(StatusCodes.Status202Accepted, job, ResourceId: job.Id);
    }

    public async Task<WateringServiceResult> ScheduleAsync(ScheduleWateringRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidPlot(request.PlotName))
            return BadRequest("plotName may contain only letters, numbers, _ and -");

        var scheduledAt = request.ScheduledAt.UtcDateTime;
        if (scheduledAt < DateTime.UtcNow.AddSeconds(-5))
            return BadRequest("scheduledAt must be in the future");

        var job = CreateJob(request.PlotName, request.DurationSeconds, "scheduled", priority: 5,
            scheduledAt);
        _db.WateringJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
        return new WateringServiceResult(StatusCodes.Status202Accepted, job, ResourceId: job.Id);
    }

    public async Task<WateringServiceResult> StopPlotAsync(string plotName,
        CancellationToken cancellationToken)
    {
        if (!ValidPlot(plotName))
            return BadRequest("Invalid plotName");

        var job = await _db.WateringJobs
            .Where(item => item.PlotName == plotName &&
                           (item.Status == "pending" || item.Status == "processing"))
            .OrderByDescending(item => item.Status == "processing")
            .ThenBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
            return new WateringServiceResult(StatusCodes.Status404NotFound,
                Message: "No pending or active watering job for this plot");

        if (job.Status == "processing")
        {
            var published = await _publisher.PublishWateringCommandAsync(
                job.PlotName, job.Id, "stop", 0, cancellationToken);
            if (!published)
                return new WateringServiceResult(StatusCodes.Status503ServiceUnavailable,
                    Message: "MQTT is disconnected; job remains processing until timeout");

            return new WateringServiceResult(StatusCodes.Status202Accepted,
                new { job.Id, status = "processing", message = "Stop command sent" });
        }

        job.Status = "cancelled";
        job.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new WateringServiceResult(StatusCodes.Status200OK, job);
    }

    public async Task<WateringServiceResult> SetAutomaticPolicyAsync(string plotName,
        AutomaticPolicyRequest request, CancellationToken cancellationToken)
    {
        if (!ValidPlot(plotName))
            return BadRequest("Invalid plotName");

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
        return new WateringServiceResult(StatusCodes.Status200OK, policy);
    }

    public async Task<WateringServiceResult> GetJobsAsync(string? plotName, int limit,
        CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 500);
        var query = _db.WateringJobs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(plotName))
        {
            if (!ValidPlot(plotName))
                return BadRequest("Invalid plotName");
            query = query.Where(job => job.PlotName == plotName);
        }

        var jobs = await query.OrderByDescending(job => job.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return new WateringServiceResult(StatusCodes.Status200OK, jobs);
    }

    public async Task<WateringServiceResult> GetJobAsync(long id,
        CancellationToken cancellationToken)
    {
        var job = await _db.WateringJobs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return job is null
            ? new WateringServiceResult(StatusCodes.Status404NotFound)
            : new WateringServiceResult(StatusCodes.Status200OK, job);
    }

    private static bool ValidPlot(string plotName) =>
        !string.IsNullOrWhiteSpace(plotName) && PlotNamePattern.IsMatch(plotName);

    private static WateringServiceResult BadRequest(string message) =>
        new(StatusCodes.Status400BadRequest, Message: message);

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
