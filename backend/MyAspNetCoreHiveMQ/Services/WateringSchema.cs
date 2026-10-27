using Microsoft.EntityFrameworkCore;
using MyAspNetCoreHiveMQ.Data;

namespace MyAspNetCoreHiveMQ.Services;

public sealed class WateringSchema : IWateringSchemaService
{
    private readonly ApplicationDbContext _db;

    public WateringSchema(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS watering_jobs (
                id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                plot_name VARCHAR(100) NOT NULL,
                duration_seconds INT NOT NULL CHECK (duration_seconds BETWEEN 1 AND 86400),
                mode VARCHAR(20) NOT NULL CHECK (mode IN ('manual', 'scheduled', 'auto')),
                status VARCHAR(20) NOT NULL DEFAULT 'pending'
                    CHECK (status IN ('pending', 'processing', 'completed', 'failed', 'cancelled')),
                priority SMALLINT NOT NULL DEFAULT 0,
                scheduled_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                dispatched_at TIMESTAMPTZ,
                started_at TIMESTAMPTZ,
                completed_at TIMESTAMPTZ,
                error_message TEXT
            );

            -- Upgrade the earlier watering_jobs shape from the SQL examples
            -- (which had valve_device_id/plot_id and sometimes status='running').
            ALTER TABLE watering_jobs ADD COLUMN IF NOT EXISTS plot_name VARCHAR(100);
            ALTER TABLE watering_jobs ADD COLUMN IF NOT EXISTS mode VARCHAR(20) NOT NULL DEFAULT 'manual';
            ALTER TABLE watering_jobs ADD COLUMN IF NOT EXISTS dispatched_at TIMESTAMPTZ;

            DO $watering_upgrade$
            DECLARE constraint_name TEXT;
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = current_schema()
                      AND table_name = 'watering_jobs' AND column_name = 'valve_device_id'
                ) THEN
                    EXECUTE 'ALTER TABLE watering_jobs ALTER COLUMN valve_device_id DROP NOT NULL';
                END IF;
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = current_schema()
                      AND table_name = 'watering_jobs' AND column_name = 'plot_id'
                ) THEN
                    EXECUTE 'UPDATE watering_jobs SET plot_name = ''plot'' || plot_id::text WHERE plot_name IS NULL AND plot_id IS NOT NULL';
                    EXECUTE 'ALTER TABLE watering_jobs ALTER COLUMN plot_id DROP NOT NULL';
                END IF;
                EXECUTE 'UPDATE watering_jobs SET plot_name = ''plot1'' WHERE plot_name IS NULL';
                EXECUTE 'ALTER TABLE watering_jobs ALTER COLUMN plot_name SET NOT NULL';

                FOR constraint_name IN
                    SELECT conname FROM pg_constraint
                    WHERE conrelid = 'watering_jobs'::regclass
                      AND contype = 'c'
                      AND pg_get_constraintdef(oid) ILIKE '%status%'
                LOOP
                    EXECUTE format('ALTER TABLE watering_jobs DROP CONSTRAINT %I', constraint_name);
                END LOOP;
                EXECUTE 'UPDATE watering_jobs SET status = ''processing'' WHERE status = ''running''';
                EXECUTE 'ALTER TABLE watering_jobs ADD CONSTRAINT ck_watering_jobs_status
                    CHECK (status IN (''pending'', ''processing'', ''completed'', ''failed'', ''cancelled''))';
            END
            $watering_upgrade$;

            CREATE INDEX IF NOT EXISTS ix_watering_jobs_pending
                ON watering_jobs (priority DESC, scheduled_at, id)
                WHERE status = 'pending';

            CREATE UNIQUE INDEX IF NOT EXISTS ux_watering_jobs_one_processing
                ON watering_jobs ((1))
                WHERE status = 'processing';

            CREATE UNIQUE INDEX IF NOT EXISTS ux_watering_jobs_one_active_auto_per_plot
                ON watering_jobs (plot_name)
                WHERE mode = 'auto' AND status IN ('pending', 'processing');

            CREATE TABLE IF NOT EXISTS watering_policies (
                plot_name VARCHAR(100) PRIMARY KEY,
                auto_enabled BOOLEAN NOT NULL DEFAULT FALSE,
                soil_threshold_percent NUMERIC(5,2) NOT NULL DEFAULT 30
                    CHECK (soil_threshold_percent BETWEEN 0 AND 100),
                duration_seconds INT NOT NULL DEFAULT 180
                    CHECK (duration_seconds BETWEEN 1 AND 86400),
                cooldown_minutes INT NOT NULL DEFAULT 60
                    CHECK (cooldown_minutes BETWEEN 0 AND 10080),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            """;

        await _db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
