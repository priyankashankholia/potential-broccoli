using Microsoft.EntityFrameworkCore;
using RentManager.Api.Data;
using RentManager.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    Environment.GetEnvironmentVariable(
        "ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException(
        "Database connection string is not configured.");

builder.Services.AddDbContext<RentManagerDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddControllers();

builder.Services.AddScoped<RentReminderService>();
builder.Services.AddHostedService<RentReminderBackgroundService>();

builder.Services.AddScoped<NotificationDeliveryService>();
builder.Services.AddHostedService<NotificationDeliveryBackgroundService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("Development");

app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    application = "RentManager API"
}));

app.Run();