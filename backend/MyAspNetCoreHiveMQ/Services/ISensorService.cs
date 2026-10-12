using MyAspNetCoreHiveMQ.Models;

namespace MyAspNetCoreHiveMQ.Services;

public interface ISensorService
{
    Task<SensorData> AddAsync(SensorData sensorData);
    Task<SensorDashboard> GetReadingsAsync(int limit);
}
