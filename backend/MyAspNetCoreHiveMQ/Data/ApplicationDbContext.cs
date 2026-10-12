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
        public DbSet<WateringJob> WateringJobs => Set<WateringJob>();
        public DbSet<WateringPolicy> WateringPolicies => Set<WateringPolicy>();

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

            modelBuilder.Entity<WateringJob>(entity =>
            {
                entity.ToTable("watering_jobs");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("id");
                entity.Property(x => x.PlotName).HasColumnName("plot_name").HasMaxLength(100).IsRequired();
                entity.Property(x => x.DurationSeconds).HasColumnName("duration_seconds");
                entity.Property(x => x.Mode).HasColumnName("mode").HasMaxLength(20).IsRequired();
                entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
                entity.Property(x => x.Priority).HasColumnName("priority");
                entity.Property(x => x.ScheduledAt).HasColumnName("scheduled_at").HasColumnType("timestamp with time zone");
                entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
                entity.Property(x => x.DispatchedAt).HasColumnName("dispatched_at").HasColumnType("timestamp with time zone");
                entity.Property(x => x.StartedAt).HasColumnName("started_at").HasColumnType("timestamp with time zone");
                entity.Property(x => x.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamp with time zone");
                entity.Property(x => x.ErrorMessage).HasColumnName("error_message");
            });

            modelBuilder.Entity<WateringPolicy>(entity =>
            {
                entity.ToTable("watering_policies");
                entity.HasKey(x => x.PlotName);
                entity.Property(x => x.PlotName).HasColumnName("plot_name").HasMaxLength(100);
                entity.Property(x => x.AutoEnabled).HasColumnName("auto_enabled");
                entity.Property(x => x.SoilThresholdPercent).HasColumnName("soil_threshold_percent").HasColumnType("numeric(5,2)");
                entity.Property(x => x.DurationSeconds).HasColumnName("duration_seconds");
                entity.Property(x => x.CooldownMinutes).HasColumnName("cooldown_minutes");
                entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
            });
        }
    }
}
