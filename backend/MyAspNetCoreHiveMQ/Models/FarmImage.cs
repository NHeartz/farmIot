namespace MyAspNetCoreHiveMQ.Models
{
    public class FarmImage
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string? CameraId { get; set; }
        public string? PlotId { get; set; }
        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    }
}
