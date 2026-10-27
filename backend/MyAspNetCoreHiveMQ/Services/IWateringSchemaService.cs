namespace MyAspNetCoreHiveMQ.Services;

public interface IWateringSchemaService
{
    Task EnsureCreatedAsync(CancellationToken cancellationToken = default);
}
