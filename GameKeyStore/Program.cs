using GameKeyStore.Services;
using GameKeyStore.Authorization;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// Load environment variables from .env file
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault;
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add Swagger services
builder.Services.AddSwaggerGen();

// Add memory cache for permissions
builder.Services.AddMemoryCache();

// Register Supabase service
builder.Services.AddSingleton<GameKeyStore.Services.SupabaseService>();

// Register Authentication service
builder.Services.AddScoped<GameKeyStore.Services.AuthService>();

// Register Permission service
builder.Services.AddScoped<GameKeyStore.Services.PermissionService>();

// Register Permission Manager service
builder.Services.AddScoped<GameKeyStore.Services.PermissionManager>();

// Register S3 service
builder.Services.AddScoped<GameKeyStore.Services.S3Service>();

// Register authorization handler
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Configure JWT Authentication
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "your-super-secret-jwt-key-that-should-be-changed-in-production";
var key = Encoding.ASCII.GetBytes(jwtSecret);

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = "GameKeyStore",
        ValidateAudience = true,
        ValidAudience = "GameKeyStore",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization(options =>
{
    // Create permission-based policies
    var permissions = new[]
    {
        // Games permissions
        ("games", "read"), ("games", "write"), ("games", "delete"), ("games", "admin"),
        // Users permissions  
        ("users", "read"), ("users", "write"), ("users", "delete"), ("users", "admin"),
        // GameKeys permissions
        ("gamekeys", "read"), ("gamekeys", "write"), ("gamekeys", "delete"), ("gamekeys", "admin"),
        // Categories permissions
        ("categories", "read"), ("categories", "write"), ("categories", "delete"), ("categories", "admin"),
        // Roles permissions
        ("roles", "read"), ("roles", "write"), ("roles", "delete"), ("roles", "admin"),
        // Orders permissions
        ("orders", "read"), ("orders", "write"), ("orders", "delete"), ("orders", "admin"),
        // S3 permissions
        ("s3", "presign"), ("s3", "delete")
    };

    foreach (var (resource, action) in permissions)
    {
        options.AddPolicy($"Permission.{resource}.{action}", policy =>
            policy.Requirements.Add(new PermissionRequirement(resource, action)));
    }

    // Role-based policies (for backward compatibility)
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
    options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("user", "admin"));
});

// Add API Explorer for Swagger
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Note: Supabase will be initialized on first use to prevent startup crashes

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "GameKeyStore API v1");
        c.SwaggerEndpoint("/openapi/v1.json", "GameKeyStore OpenAPI v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseRouting();

// app.UseHttpsRedirection(); // Disabled for HTTP-only development

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Optional: Keep a simple weatherforecast endpoint for backward compatibility
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast")
    .WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}