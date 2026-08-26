using System;
using System.Buffers;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MyAspNetCoreHiveMQ.Data;
using MyAspNetCoreHiveMQ.Models;

namespace MyAspNetCoreHiveMQ.Services;

public class HiveMqttService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private IMqttClient? _mqttClient;

    public HiveMqttService(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var broker = _configuration["HiveMQ:Broker"]!;
        var port = _configuration.GetValue<int>("HiveMQ:Port");
        var username = _configuration["HiveMQ:Username"]!;
        var password = _configuration["HiveMQ:Password"]!;
        var clientId = _configuration["HiveMQ:ClientId"]!;
        var topic = _configuration["HiveMQ:Topics:0"]!;

        var factory = new MqttClientFactory();
        _mqttClient = factory.CreateMqttClient();

        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

        var options = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(broker, port)
            .WithCredentials(username, password)
            .WithTlsOptions(tls =>
            {
                tls.UseTls();

                // ใช้ทดสอบชั่วคราวเท่านั้น
                tls.WithAllowUntrustedCertificates(true);
                tls.WithIgnoreCertificateChainErrors(true);
                tls.WithIgnoreCertificateRevocationErrors(true);
                tls.WithCertificateValidationHandler(_ => true);
            })
            .Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_mqttClient.IsConnected)
                {
                    await _mqttClient.ConnectAsync(options, stoppingToken);

                    await _mqttClient.SubscribeAsync(
                        new MqttTopicFilterBuilder()
                            .WithTopic(topic)
                            .Build(),
                        stoppingToken);

                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MQTT error: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task OnMessageReceivedAsync(
        MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(
            e.ApplicationMessage.Payload.ToArray());

        if (!decimal.TryParse(
                payload.Trim(),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var sensorValue))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var data = new SensorData
        {
            Topic = topic,
            SensorValue = sensorValue,
            CreatedAt = DateTime.UtcNow
        };

        context.SensorData.Add(data);
        await context.SaveChangesAsync();

    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_mqttClient?.IsConnected == true)
        {
            await _mqttClient.DisconnectAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}