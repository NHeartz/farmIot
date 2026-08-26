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
        }
    }
}