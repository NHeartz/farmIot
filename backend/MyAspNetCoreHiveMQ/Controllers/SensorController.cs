using Microsoft.AspNetCore.Mvc;
using MyAspNetCoreHiveMQ.Models;
using MyAspNetCoreHiveMQ.Services;

namespace MyAspNetCoreHiveMQ.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SensorController : ControllerBase
{
    private readonly ISensorService _sensorService;

    public SensorController(ISensorService sensorService)
    {
        _sensorService = sensorService;
    }

    [HttpPost]
    public async Task<IActionResult> PostSensorData([FromBody] SensorData sensorData)
    {
        if (sensorData == null)
            return BadRequest("Sensor data is null.");

        var savedSensorData = await _sensorService.AddAsync(sensorData);
        return CreatedAtAction(nameof(PostSensorData), new { id = savedSensorData.Id }, savedSensorData);
    }

    [HttpGet]
    public async Task<IActionResult> GetSensorData([FromQuery] int limit = 60)
    {
        return Ok(await _sensorService.GetReadingsAsync(limit));
    }
}
