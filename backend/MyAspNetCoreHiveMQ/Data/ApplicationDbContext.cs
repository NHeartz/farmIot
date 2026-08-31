using Microsoft.EntityFrameworkCore;
using MyAspNetCoreHiveMQ.Models;

namespace MyAspNetCoreHiveMQ.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<SensorData> SensorData => Set<SensorData>();
        public DbSet<FarmImage> FarmImages => Set<FarmImage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SensorData>(entity =>
            {
                entity.ToTable("sensordata");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .HasColumnName("id");

                entity.Property(x => x.Topic)
                    .HasColumnName("topic")
                    .HasMaxLength(255)
                    .IsRequired();

                entity.Property(x => x.SensorValue)
                    .HasColumnName("sensorvalue")
                    .HasColumnType("numeric(10,2)")
                    .IsRequired();

                entity.Property(x => x.CreatedAt)
                    .HasColumnName("createdat")
                    .HasColumnType("timestamp with time zone");
            });

            modelBuilder.Entity<FarmImage>(entity =>
            {
                entity.ToTable("farmimages");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .HasColumnName("id");

                entity.Property(x => x.FileName)
                    .HasColumnName("filename")
                    .HasMaxLength(255)
                    .IsRequired();

                entity.Property(x => x.ImageUrl)
                    .HasColumnName("image_url")
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(x => x.CameraId)
                    .HasColumnName("camera_id")
                    .HasMaxLength(100);

                entity.Property(x => x.PlotId)
                    .HasColumnName("plot_id")
                    .HasMaxLength(100);

                entity.Property(x => x.CapturedAt)
                    .HasColumnName("captured_at")
                    .HasColumnType("timestamp with time zone");
            });
        }
    }
}