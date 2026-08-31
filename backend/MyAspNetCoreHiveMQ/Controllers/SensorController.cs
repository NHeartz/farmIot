using Microsoft.AspNetCore.Mvc;
using MyAspNetCoreHiveMQ.Data;
using MyAspNetCoreHiveMQ.Models;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace MyAspNetCoreHiveMQ.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SensorController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SensorController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> PostSensorData([FromBody] SensorData sensorData)
        {
            if (sensorData == null)
            {
                return BadRequest("Sensor data is null.");
            }

            _context.SensorData.Add(sensorData);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(PostSensorData), new { id = sensorData.Id }, sensorData);
        }

        [HttpGet]
        public async Task<IActionResult> GetSensorData([FromQuery] int limit = 60)
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

            return Ok(new
            {
                latest = readings.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.LastOrDefault()),
                history = readings
            });
        }

        private static string? GetSensorType(string topic)
        {
            var name = topic.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.ToLowerInvariant();

            return name switch
            {
                "temperature" or "temp" => "temperature",
                "soil_humid" or "soilhumid" or "soil_humidity" or "soil_moisture" or "soilmoisture" => "humidity",
                "air_humid" or "airhumid" or "air_humidity" => "air_humidity",
                "humidity" or "hum" => "humidity",
                _ => null
            };
        }

        private sealed record SensorReading(
            [property: JsonPropertyName("type")] string? Type,
            [property: JsonPropertyName("value")] decimal Value,
            [property: JsonPropertyName("createdAt")] DateTime CreatedAt);
    }
}