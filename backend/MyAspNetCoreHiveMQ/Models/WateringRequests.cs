using System.ComponentModel.DataAnnotations;

namespace MyAspNetCoreHiveMQ.Models;

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
