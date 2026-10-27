using MyAspNetCoreHiveMQ.Models;

namespace MyAspNetCoreHiveMQ.Services;

public interface IWateringService
{
    Task<WateringServiceResult> StartManualAsync(CreateWateringRequest request,
        CancellationToken cancellationToken);
    Task<WateringServiceResult> ScheduleAsync(ScheduleWateringRequest request,
        CancellationToken cancellationToken);
    Task<WateringServiceResult> StopPlotAsync(string plotName, CancellationToken cancellationToken);
    Task<WateringServiceResult> SetAutomaticPolicyAsync(string plotName,
        AutomaticPolicyRequest request, CancellationToken cancellationToken);
    Task<WateringServiceResult> GetJobsAsync(string? plotName, int limit,
        CancellationToken cancellationToken);
    Task<WateringServiceResult> GetJobAsync(long id, CancellationToken cancellationToken);
}

public sealed record WateringServiceResult(int StatusCode, object? Value = null,
    string? Message = null, long? ResourceId = null);
