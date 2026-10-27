using Microsoft.EntityFrameworkCore;
using MyAspNetCoreHiveMQ.Data;
using MyAspNetCoreHiveMQ.Models;

namespace MyAspNetCoreHiveMQ.Services;

public sealed class WateringQueueService : IWateringQueueService
{
    private readonly ApplicationDbContext _db;
    private readonly IMqttCommandPublisher _publisher;
    private readonly ILogger<WateringQueueService> _logger;

    public WateringQueueService(ApplicationDbContext db, IMqttCommandPublisher publisher,
        ILogger<WateringQueueService> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        await FailExpiredJobAsync(cancellationToken);
        await DispatchNextAsync(cancellationToken);
    }

    public async Task EnqueueIfNeededAsync(string plotName, decimal soilPercent,
        CancellationToken cancellationToken)
    {
        var policy = await _db.WateringPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.PlotName == plotName && item.AutoEnabled, cancellationToken);

        if (policy is null || soilPercent > policy.SoilThresholdPercent)
        {
            return;
        }

        var activeJobExists = await _db.WateringJobs.AnyAsync(job =>
            job.PlotName == plotName && (job.Status == "pending" || job.Status == "processing"),
            cancellationToken);

        if (activeJobExists)
        {
            return;
        }

        var latestAutoJob = await _db.WateringJobs
            .AsNoTracking()
            .Where(job => job.PlotName == plotName && job.Mode == "auto")
            .OrderByDescending(job => job.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestAutoJob is not null && policy.CooldownMinutes > 0)
        {
            var cooldownFrom = latestAutoJob.CompletedAt ?? latestAutoJob.CreatedAt;
            if (cooldownFrom.AddMinutes(policy.CooldownMinutes) > DateTime.UtcNow)
            {
                return;
            }
        }

        _db.WateringJobs.Add(new WateringJob
        {
            PlotName = plotName,
            DurationSeconds = policy.DurationSeconds,
            Mode = "auto",
            Status = "pending",
            Priority = 0,
            ScheduledAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The partial unique index prevents concurrent sensor messages
            // from creating multiple auto jobs for the same plot.
        }
    }

    private async Task DispatchNextAsync(CancellationToken cancellationToken)
    {
        // The update and row selection happen in one statement. The unique
        // partial index is a second guard if multiple backend instances run.
        const string claimSql = """
            WITH candidate AS (
                SELECT id
                FROM watering_jobs
                WHERE status = 'pending'
                  AND scheduled_at <= CURRENT_TIMESTAMP
                  AND NOT EXISTS (SELECT 1 FROM watering_jobs WHERE status = 'processing')
                ORDER BY priority DESC, scheduled_at ASC, id ASC
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            UPDATE watering_jobs AS job
            SET status = 'processing', dispatched_at = CURRENT_TIMESTAMP, error_message = NULL
            FROM candidate
            WHERE job.id = candidate.id
            RETURNING job.id, job.plot_name, job.duration_seconds, job.mode, job.status,
                      job.priority, job.scheduled_at, job.created_at, job.dispatched_at,
                      job.started_at, job.completed_at, job.error_message;
            """;

        WateringJob? job = null;
        await _db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = _db.Database.GetDbConnection().CreateCommand();
            command.CommandText = claimSql;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                job = new WateringJob
                {
                    Id = reader.GetInt64(0),
                    PlotName = reader.GetString(1),
                    DurationSeconds = reader.GetInt32(2),
                    Mode = reader.GetString(3),
                    Status = reader.GetString(4),
                    Priority = reader.GetInt16(5),
                    ScheduledAt = reader.GetDateTime(6),
                    CreatedAt = reader.GetDateTime(7),
                    DispatchedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                    StartedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    CompletedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                    ErrorMessage = reader.IsDBNull(11) ? null : reader.GetString(11)
                };
            }
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }

        if (job is null)
        {
            return;
        }

        var published = await _publisher.PublishWateringCommandAsync(
            job.PlotName,
            job.Id,
            "start",
            job.DurationSeconds,
            cancellationToken);

        if (!published)
        {
            await _db.WateringJobs
                .Where(item => item.Id == job.Id && item.Status == "processing")
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.Status, "pending")
                    .SetProperty(item => item.DispatchedAt, (DateTime?)null), cancellationToken);
        }
    }

    private async Task FailExpiredJobAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expired = await _db.WateringJobs
            .AsNoTracking()
            .Where(job => job.Status == "processing" && job.DispatchedAt != null)
            .FirstOrDefaultAsync(job => job.DispatchedAt!.Value.AddSeconds(job.DurationSeconds + 90) <= now,
                cancellationToken);

        if (expired is null)
        {
            return;
        }

        await _publisher.PublishWateringCommandAsync(
            expired.PlotName, expired.Id, "stop", 0, cancellationToken);

        await _db.WateringJobs
            .Where(job => job.Id == expired.Id && job.Status == "processing")
            .ExecuteUpdateAsync(update => update
                .SetProperty(job => job.Status, "failed")
                .SetProperty(job => job.CompletedAt, now)
                .SetProperty(job => job.ErrorMessage, "No terminal status received before timeout"),
                cancellationToken);

        _logger.LogWarning("Watering job {JobId} timed out", expired.Id);
    }
}
