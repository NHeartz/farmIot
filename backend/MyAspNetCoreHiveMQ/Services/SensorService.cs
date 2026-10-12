using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MyAspNetCoreHiveMQ.Data;
using MyAspNetCoreHiveMQ.Models;

namespace MyAspNetCoreHiveMQ.Services;

public sealed class SensorService : ISensorService
{
    private readonly ApplicationDbContext _context;

    public SensorService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SensorData> AddAsync(SensorData sensorData)
    {
        _context.SensorData.Add(sensorData);
        await _context.SaveChangesAsync();
        return sensorData;
    }

    public async Task<SensorDashboard> GetReadingsAsync(int limit)
    {
        limit = Math.Clamp(limit, 1, 500);

        var rows = await _context.SensorData
            .AsNoTracking()
            .OrderByDescending(sensor => sensor.CreatedAt)
            .Take(limit * 4)
            .ToListAsync();

        var readings = rows
            .Select(sensor => new SensorReading(
                GetSensorType(sensor.Topic),
                sensor.SensorValue,
                sensor.CreatedAt))
            .Where(reading => reading.Type is not null)
            .OrderBy(reading => reading.CreatedAt)
            .GroupBy(reading => reading.Type)
            .ToDictionary(
                group => group.Key!,
                group => group.TakeLast(limit).ToArray());

        return new SensorDashboard(
            readings.ToDictionary(pair => pair.Key, pair => pair.Value.LastOrDefault()),
            readings);
    }

    private static string? GetSensorType(string topic)
    {
        var name = topic.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.ToLowerInvariant();

        return name switch
        {
            "temperature" or "temp" => "temperature",
            "soil_humid" or "soilhumid" or "soil_humidity" or "soil_moisture" or "soilmoisture" => "humidity",
            "soil_ph" or "soilph" or "ph" => "soil_ph",
            "air_humid" or "airhumid" or "air_humidity" => "air_humidity",
            "water_flow" or "waterflow" => "water_flow",
            "humidity" or "hum" => "humidity",
            _ => null
        };
    }
}

public sealed record SensorDashboard(
    [property: JsonPropertyName("latest")] IReadOnlyDictionary<string, SensorReading?> Latest,
    [property: JsonPropertyName("history")] IReadOnlyDictionary<string, SensorReading[]> History);

public sealed record SensorReading(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("value")] decimal Value,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt);
