using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Protocol;
using MyAspNetCoreHiveMQ.Data;
using MyAspNetCoreHiveMQ.Models;

namespace MyAspNetCoreHiveMQ.Services;

public sealed class HiveMqttService : BackgroundService, IMqttCommandPublisher
{
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HiveMqttService> _logger;
    private IMqttClient? _mqttClient;

    public HiveMqttService(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<HiveMqttService> logger)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var broker = _configuration["HiveMQ:Broker"];
        var port = _configuration.GetValue<int>("HiveMQ:Port", 8883);
        var username = _configuration["HiveMQ:Username"];
        var password = _configuration["HiveMQ:Password"];
        var clientId = _configuration["HiveMQ:ClientId"] ?? "FarmBackend-Csharp";
        var sensorTopic = _configuration["HiveMQ:Topics:0"] ?? "farm/+/+";

        if (string.IsNullOrWhiteSpace(broker) || string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            _logger.LogError("MQTT settings are missing; MQTT service will not connect");
            return;
        }

        _mqttClient = new MqttClientFactory().CreateMqttClient();
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

        var options = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(broker, port)
            .WithCredentials(username, password)
            .WithTlsOptions(tls => tls.UseTls())
            .Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_mqttClient.IsConnected)
                {
                    await _mqttClient.ConnectAsync(options, stoppingToken);

                    foreach (var topic in new[] { sensorTopic, "farm/+/valve/status" }.Distinct())
                    {
                        await _mqttClient.SubscribeAsync(
                            new MqttTopicFilterBuilder().WithTopic(topic).Build(),
                            stoppingToken);
                    }

                    _logger.LogInformation("Connected to MQTT broker and subscribed to {SensorTopic} and valve status", sensorTopic);
                }

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "MQTT connection or subscription failed");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray());

        if (topic.EndsWith("/valve/status", StringComparison.Ordinal))
        {
            await HandleValveStatusAsync(topic, payload);
            return;
        }

        if (!decimal.TryParse(payload.Trim(), NumberStyles.Any,
                CultureInfo.InvariantCulture, out var sensorValue))
        {
            return;
        }

        var parts = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !parts[0].Equals("farm", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.SensorData.Add(new SensorData
        {
            Topic = topic,
            SensorValue = sensorValue,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        if (parts[2].Equals("soil_humid", StringComparison.OrdinalIgnoreCase))
        {
            var wateringQueue = scope.ServiceProvider.GetRequiredService<IWateringQueueService>();
            await wateringQueue.EnqueueIfNeededAsync(parts[1], sensorValue, CancellationToken.None);
        }
    }

    private async Task HandleValveStatusAsync(string topic, string payload)
    {
        DeviceStatus? message;
        try
        {
            message = JsonSerializer.Deserialize<DeviceStatus>(payload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Invalid valve status payload on {Topic}", topic);
            return;
        }

        var parts = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || message is null || message.JobId <= 0 ||
            string.IsNullOrWhiteSpace(message.Status))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var job = await context.WateringJobs.FirstOrDefaultAsync(item =>
            item.Id == message.JobId && item.PlotName == parts[1] && item.Status == "processing");

        if (job is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        switch (message.Status.ToLowerInvariant())
        {
            case "started":
                job.StartedAt ??= now;
                break;
            case "completed":
                job.Status = "completed";
                job.StartedAt ??= now;
                job.CompletedAt = now;
                break;
            case "stopped":
                job.Status = "cancelled";
                job.CompletedAt = now;
                break;
            case "rejected":
                job.Status = "failed";
                job.CompletedAt = now;
                job.ErrorMessage = message.Detail ?? "Device rejected the watering job";
                break;
            default:
                return;
        }

        await context.SaveChangesAsync();
    }

    public async Task<bool> PublishWateringCommandAsync(
        string plotName,
        long jobId,
        string action,
        int durationSeconds,
        CancellationToken cancellationToken = default)
    {
        if (_mqttClient?.IsConnected != true)
        {
            return false;
        }

        var payload = JsonSerializer.Serialize(new { jobId, action, durationSeconds });
        var message = new MqttApplicationMessageBuilder()
            .WithTopic($"farm/{plotName}/valve/control")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(false)
            .Build();

        await _mqttClient.PublishAsync(message, cancellationToken);
        return true;
    }

    private sealed record DeviceStatus(long JobId, string Status, string? Detail);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_mqttClient?.IsConnected == true)
        {
            await _mqttClient.DisconnectAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
