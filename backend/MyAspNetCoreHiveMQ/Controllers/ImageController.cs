using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyAspNetCoreHiveMQ.Data;
using MyAspNetCoreHiveMQ.Models;
using Supabase;

[Route("api/[controller]")]
[ApiController]
public class ImageController : ControllerBase
{
    private readonly Client _supabaseClient;
    private readonly ApplicationDbContext _context;

    public ImageController(Client supabaseClient, ApplicationDbContext context)
    {
        _supabaseClient = supabaseClient;
        _context = context;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadImage(IFormFile file, [FromForm] string? cameraId = null, [FromForm] string? plotId = null)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No image attached.");

        var normalizedCameraId = NormalizeId(cameraId, "unknown-camera");
        var normalizedPlotId = NormalizeId(plotId, "unknown-plot");

        var bucket = _supabaseClient.Storage.From("farm-images");
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);

        var fileName = $"farm_image_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}.jpg";
        var fileBytes = memoryStream.ToArray();

        await bucket.Upload(fileBytes, fileName);

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

        return Ok(new
        {
            message = "Upload Success",
            url = publicUrl,
            fileName,
            cameraId = farmImage.CameraId,
            plotId = farmImage.PlotId,
            bucket = "farm-images",
            savedToDatabase = true
        });
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatestImages([FromQuery] string? plotId = null, [FromQuery] List<string>? cameraIds = null)
    {
        var requestedCameras = (cameraIds ?? new List<string>())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => CanonicalCameraId(v))
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requestedCameras.Count == 0)
        {
            requestedCameras = new List<string> { "cam_01", "cam_02", "cam_03", "cam_04" };
        }

        var normalizedPlotId = CanonicalPlotId(plotId);

        var allImages = await _context.FarmImages
            .AsNoTracking()
            .Where(x => x.CameraId != null)
            .OrderByDescending(x => x.CapturedAt)
            .ToListAsync();

        var latestRows = allImages
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.CameraId) &&
                requestedCameras.Contains(CanonicalCameraId(x.CameraId), StringComparer.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(normalizedPlotId) ||
                 string.Equals(CanonicalPlotId(x.PlotId), normalizedPlotId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var latestByCamera = latestRows
            .GroupBy(x => CanonicalCameraId(x.CameraId!))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.CapturedAt).First(),
                StringComparer.OrdinalIgnoreCase);

        var result = new List<object>();

        foreach (var cameraId in requestedCameras)
        {
            var image = latestByCamera.TryGetValue(cameraId, out var latestImage)
                ? latestImage
                : null;

            result.Add(image == null
                ? new
                {
                    cameraId = cameraId,
                    plotId = normalizedPlotId,
                    imageUrl = (string?)null,
                    capturedAt = (DateTime?)null
                }
                : new
                {
                    cameraId = CanonicalCameraId(image.CameraId),
                    plotId = CanonicalPlotId(image.PlotId),
                    imageUrl = image.ImageUrl,
                    capturedAt = image.CapturedAt
                });
        }

        return Ok(new
        {
            plotId = normalizedPlotId,
            cameras = result
        });
    }

    private static string CanonicalCameraId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var cleaned = value.Trim();
        if (cleaned.Contains("_"))
        {
            var parts = cleaned.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2 && parts[0].Equals("cam", StringComparison.OrdinalIgnoreCase))
            {
                return $"cam_{int.Parse(parts[1]).ToString("00")}";
            }
        }

        var match = System.Text.RegularExpressions.Regex.Match(cleaned, @"(?i)(?:cam|camera)[^0-9]*(\d+)");
        if (match.Success)
        {
            return $"cam_{int.Parse(match.Groups[1].Value).ToString("00")}";
        }

        return cleaned;
    }

    private static string CanonicalPlotId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var cleaned = value.Trim();
        if (cleaned.Contains("_"))
        {
            var parts = cleaned.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2 && parts[0].Equals("plot", StringComparison.OrdinalIgnoreCase))
            {
                return $"plot_{int.Parse(parts[1]).ToString("00")}";
            }
        }

        var match = System.Text.RegularExpressions.Regex.Match(cleaned, @"(?i)(?:plot)[^0-9]*(\d+)");
        if (match.Success)
        {
            return $"plot_{int.Parse(match.Groups[1].Value).ToString("00")}";
        }

        return cleaned;
    }

    private static string NormalizeId(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var trimmed = value.Trim();

        if (trimmed.StartsWith("cam_", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("camera", StringComparison.OrdinalIgnoreCase))
            return CanonicalCameraId(trimmed);

        if (trimmed.StartsWith("plot_", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("plot", StringComparison.OrdinalIgnoreCase))
            return CanonicalPlotId(trimmed);

        return trimmed;
    }
}