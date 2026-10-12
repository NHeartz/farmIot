namespace MyAspNetCoreHiveMQ.Models;

public sealed class WateringJob
{
    public long Id { get; set; }
    public string PlotName { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public string Mode { get; set; } = "manual";
    public string Status { get; set; } = "pending";
    public short Priority { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
