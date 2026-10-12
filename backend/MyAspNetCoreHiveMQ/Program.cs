using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using MyAspNetCoreHiveMQ.Data;
using MyAspNetCoreHiveMQ.Services;
using Supabase;

var builder = WebApplication.CreateBuilder(args);

var listenUrls = builder.Configuration["Urls"] ?? "http://0.0.0.0:5000;https://0.0.0.0:5001";
builder.WebHost.UseUrls(listenUrls);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MinRequestBodyDataRate = null;
    options.Limits.MinResponseDataRate = null;
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ISensorService, SensorService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddSingleton<HiveMqttService>();
builder.Services.AddSingleton<IMqttCommandPublisher>(serviceProvider =>
    serviceProvider.GetRequiredService<HiveMqttService>());
builder.Services.AddHostedService<HiveMqttService>(serviceProvider =>
    serviceProvider.GetRequiredService<HiveMqttService>());
builder.Services.AddHostedService<WateringQueueWorker>();

var supabaseUrl = builder.Configuration["Supabase:Url"];
var supabaseKey = builder.Configuration["Supabase:ServiceRoleKey"]
    ?? builder.Configuration["Supabase:Key"];

if (!string.IsNullOrWhiteSpace(supabaseUrl) && !string.IsNullOrWhiteSpace(supabaseKey))
{
    builder.Services.AddSingleton(_ => new Client(supabaseUrl, supabaseKey));
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("Frontend");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.EnsureCreatedAsync();
    await WateringSchema.EnsureCreatedAsync(db);
}

app.MapControllers();

app.Run();
