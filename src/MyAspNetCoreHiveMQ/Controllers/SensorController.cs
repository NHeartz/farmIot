using Microsoft.AspNetCore.Mvc;
using MyAspNetCoreHiveMQ.Data;
using MyAspNetCoreHiveMQ.Models;
using System.Threading.Tasks;

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
    }
}