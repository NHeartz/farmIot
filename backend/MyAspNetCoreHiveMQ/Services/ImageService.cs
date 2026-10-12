using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MyAspNetCoreHiveMQ.Data;
using MyAspNetCoreHiveMQ.Models;
using Supabase;

namespace MyAspNetCoreHiveMQ.Services;

public sealed class ImageService : IImageService
{
    private readonly Client _supabaseClient;
    private readonly ApplicationDbContext _context;

    public ImageService(Client supabaseClient, ApplicationDbContext context)
    {
        _supabaseClient = supabaseClient;
        _context = context;
    }

    public async Task<ImageUploadResult> UploadAsync(IFormFile file, string? cameraId, string? plotId)
    {
        var normalizedCameraId = NormalizeId(cameraId, "unknown-camera");
        var normalizedPlotId = NormalizeId(plotId, "unknown-plot");
        var bucket = _supabaseClient.Storage.From("farm-images");

        await using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);

        var fileName = $"farm_image_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}.jpg";
        await bucket.Upload(memoryStream.ToArray(), fileName);

        var publicUrl = bucket.GetPublicUrl(fileName);
        var farmImage = new FarmImage
        {
            FileName = fileName,
            ImageUrl = publicUrl,
            CameraId = normalizedCameraId,
            PlotId = normalizedPlotId,
            CapturedAt = DateTime.UtcNow
        };

        _context.FarmImages.Add(farmImage);
        await _context.SaveChangesAsync();

        return new ImageUploadResult("Upload Success", publicUrl, fileName, farmImage.CameraId, farmImage.PlotId, "farm-images", true);
    }

    public async Task<LatestImagesResult> GetLatestImagesAsync(string? plotId, IEnumerable<string>? cameraIds)
    {
        var requestedCameras = (cameraIds ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(CanonicalCameraId)
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requestedCameras.Count == 0)
            requestedCameras = ["cam_01", "cam_02", "cam_03", "cam_04"];

        var normalizedPlotId = CanonicalPlotId(plotId);
        var allImages = await _context.FarmImages
            .AsNoTracking()
            .Where(image => image.CameraId != null)
            .OrderByDescending(image => image.CapturedAt)
            .ToListAsync();

        var latestByCamera = allImages
            .Where(image =>
                !string.IsNullOrWhiteSpace(image.CameraId) &&
                requestedCameras.Contains(CanonicalCameraId(image.CameraId), StringComparer.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(normalizedPlotId) ||
                 string.Equals(CanonicalPlotId(image.PlotId), normalizedPlotId, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(image => CanonicalCameraId(image.CameraId!))
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(image => image.CapturedAt).First(),
                StringComparer.OrdinalIgnoreCase);

        var cameras = requestedCameras.Select(cameraId =>
        {
            if (!latestByCamera.TryGetValue(cameraId, out var image))
                return new LatestCameraImage(cameraId, normalizedPlotId, null, null);

            return new LatestCameraImage(
                CanonicalCameraId(image.CameraId),
                CanonicalPlotId(image.PlotId),
                image.ImageUrl,
                image.CapturedAt);
        }).ToArray();

        return new LatestImagesResult(normalizedPlotId, cameras);
    }

    private static string CanonicalCameraId(string? value) => CanonicalId(value, "cam", "camera");
    private static string CanonicalPlotId(string? value) => CanonicalId(value, "plot");

    private static string CanonicalId(string? value, string prefix, params string[] aliases)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var cleaned = value.Trim();
        var names = new[] { prefix }.Concat(aliases);
        var match = Regex.Match(cleaned, $@"(?i)(?:{string.Join("|", names)})[^0-9]*(\d+)");

        return match.Success && int.TryParse(match.Groups[1].Value, out var number)
            ? $"{prefix}_{number:00}"
            : cleaned;
    }

    private static string NormalizeId(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;

        var trimmed = value.Trim();
        if (trimmed.StartsWith("cam_", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("camera", StringComparison.OrdinalIgnoreCase))
            return CanonicalCameraId(trimmed);
        if (trimmed.StartsWith("plot_", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("plot", StringComparison.OrdinalIgnoreCase))
            return CanonicalPlotId(trimmed);

        return trimmed;
    }
}

public sealed record ImageUploadResult(string Message, string Url, string FileName, string? CameraId, string? PlotId, string Bucket, bool SavedToDatabase);
public sealed record LatestImagesResult(string PlotId, IReadOnlyList<LatestCameraImage> Cameras);
public sealed record LatestCameraImage(string CameraId, string PlotId, string? ImageUrl, DateTime? CapturedAt);
