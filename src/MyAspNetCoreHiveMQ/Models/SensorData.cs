using System;

namespace MyAspNetCoreHiveMQ.Models
{
    public class SensorData
    {
        public int Id { get; set; }
        public string Topic { get; set; } = string.Empty;
        public decimal SensorValue { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}