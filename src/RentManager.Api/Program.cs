using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RentManager.Api.Data;
using RentManager.Api.Security;
using RentManager.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Read through IConfiguration so a Codespaces secret named
// ConnectionStrings__DefaultConnection is picked up automatically.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string is not configured. Set the Codespaces " +
        "secret ConnectionStrings__DefaultConnection. See README-SETUP.md.");
}

builder.Services.AddDbContext<RentManagerDbContext>(options =>
    options.UseNpgsql(connectionString));

var jwtOptions = new JwtOptions();
builder.Configuration.GetSection(JwtOptions.SectionName).Bind(jwtOptions);

if (string.IsNullOrWhiteSpace(jwtOptions.Key))
{
    throw new InvalidOperationException(
        "Jwt__Key is not configured. Set it as a Codespaces secret. " +
        "See README-SETUP.md.");
}

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<JwtTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.FromMinutes(1),

            // Explicit so User.Identity.Name is always the username.
            NameClaimType = JwtOptions.UsernameClaim
        };

        options.MapInboundClaims = false;
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();

builder.Services.AddScoped<RentLedgerService>();
builder.Services.AddScoped<RentGenerationService>();
builder.Services.AddHostedService<RentGenerationBackgroundService>();

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RentManagerDbContext>();

    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");

    await db.Database.MigrateAsync();

    await LandlordSeeder.SeedAsync(db, app.Configuration, logger);
}

app.UseCors("Development");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Left open so the container can be probed without a token.
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    application = "RentManager API"
}));

app.Run();
