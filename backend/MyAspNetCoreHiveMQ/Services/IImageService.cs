using Microsoft.AspNetCore.Http;

namespace MyAspNetCoreHiveMQ.Services;

public interface IImageService
{
    Task<ImageUploadResult> UploadAsync(IFormFile file, string? cameraId, string? plotId);
    Task<LatestImagesResult> GetLatestImagesAsync(string? plotId, IEnumerable<string>? cameraIds);
}
