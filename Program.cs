using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using CookWatch.API.Hubs;
using CookWatch.API.Middleware;
using CookWatch.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration from environment variables (Railway, etc.)
var mongoUri = Environment.GetEnvironmentVariable("MONGODB_URI");
if (!string.IsNullOrEmpty(mongoUri))
    builder.Configuration["MongoDB:ConnectionString"] = mongoUri;

var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
if (!string.IsNullOrEmpty(jwtSecret))
    builder.Configuration["Jwt:Secret"] = jwtSecret;

var clientUrl = Environment.GetEnvironmentVariable("CLIENT_URL")
    ?? builder.Configuration["App:ClientUrl"]
    ?? "http://localhost:5173";

// Increase request body limit to 6MB (for base64 images)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 6 * 1024 * 1024; // 6MB
});

// Services
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<BroadcastService>();

// SignalR
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 6 * 1024 * 1024;
});

// Background service for auto-completing expired orders
builder.Services.AddHostedService<OrderAutoCompleteService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(clientUrl)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// JWT Authentication
var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT_SECRET is not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // Allow JWT from query string for SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never;
    });

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Initialize MongoDB indexes
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MongoDbService>();
    try
    {
        await db.CreateIndexesAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Failed to create MongoDB indexes. They may already exist.");
    }
}

// Middleware pipeline
app.UseMiddleware<ExceptionMiddleware>();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    timestamp = DateTime.UtcNow,
    service = "CookWatch API"
}));

app.MapControllers();

// SignalR hub
app.MapHub<CookWatchHub>("/hub");

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://+:{port}");

app.Run();
