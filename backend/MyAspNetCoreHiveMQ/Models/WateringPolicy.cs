namespace MyAspNetCoreHiveMQ.Models;

public sealed class WateringPolicy
{
    public string PlotName { get; set; } = string.Empty;
    public bool AutoEnabled { get; set; }
    public decimal SoilThresholdPercent { get; set; }
    public int DurationSeconds { get; set; }
    public int CooldownMinutes { get; set; }
    public DateTime UpdatedAt { get; set; }
}
