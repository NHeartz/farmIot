using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using MyAspNetCoreHiveMQ.Models;
using MyAspNetCoreHiveMQ.Services;

namespace MyAspNetCoreHiveMQ.Tests
{
    public class SensorServiceTests
    {
        private readonly Mock<DbSet<SensorData>> _mockSet;
        private readonly Mock<ApplicationDbContext> _mockContext;
        private readonly HiveMqttService _service;

        public SensorServiceTests()
        {
            _mockSet = new Mock<DbSet<SensorData>>();
            _mockContext = new Mock<ApplicationDbContext>();
            _service = new HiveMqttService(_mockContext.Object);
        }

        [Fact]
        public async Task AddSensorData_ShouldAddDataToDatabase()
        {
            // Arrange
            var sensorData = new SensorData
            {
                Topic = "test/topic",
                SensorValue = 25.50m,
                CreatedAt = DateTime.UtcNow
            };

            _mockContext.Setup(m => m.Set<SensorData>()).Returns(_mockSet.Object);
            _mockContext.Setup(m => m.SaveChangesAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(1);

            // Act
            await _service.AddSensorData(sensorData);

            // Assert
            _mockSet.Verify(m => m.Add(It.IsAny<SensorData>()), Times.Once);
            _mockContext.Verify(m => m.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}