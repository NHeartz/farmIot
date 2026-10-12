namespace MyAspNetCoreHiveMQ.Services;

public interface IMqttCommandPublisher
{
    Task<bool> PublishWateringCommandAsync(
        string plotName,
        long jobId,
        string action,
        int durationSeconds,
        CancellationToken cancellationToken = default);
}
