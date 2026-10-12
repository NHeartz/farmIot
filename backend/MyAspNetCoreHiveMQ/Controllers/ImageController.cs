using Microsoft.AspNetCore.Mvc;
using MyAspNetCoreHiveMQ.Services;

[Route("api/[controller]")]
[ApiController]
public class ImageController : ControllerBase
{
    private readonly IImageService _imageService;

    public ImageController(IImageService imageService)
    {
        _imageService = imageService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadImage(IFormFile file, [FromForm] string? cameraId = null, [FromForm] string? plotId = null)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No image attached.");

        return Ok(await _imageService.UploadAsync(file, cameraId, plotId));
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatestImages([FromQuery] string? plotId = null, [FromQuery] List<string>? cameraIds = null)
    {
        return Ok(await _imageService.GetLatestImagesAsync(plotId, cameraIds));
    }
}
