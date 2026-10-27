namespace MyAspNetCoreHiveMQ.Services;

public interface IWateringQueueService
{
    Task ProcessQueueAsync(CancellationToken cancellationToken);
    Task EnqueueIfNeededAsync(string plotName, decimal soilPercent,
        CancellationToken cancellationToken);
}
