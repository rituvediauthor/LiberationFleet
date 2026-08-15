using LiberationFleet.Server.Api.Exceptions;
using LiberationFleet.Server.Application;
using LiberationFleet.Server.Hubs;
using LiberationFleet.Server.Infrastructure;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Allow large media uploads (binary ciphertext/plain up to ~640 MB for ~600 MB plain files).
const long maxMediaBodyBytes = 640L * 1024 * 1024;
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxMediaBodyBytes;
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxMediaBodyBytes;
});
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = maxMediaBodyBytes;
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
var key = Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"] ?? "LiberationFleet",
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"] ?? "LiberationFleetClient",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                // SignalR hubs and HTML5 media elements cannot set Authorization headers.
                if (!string.IsNullOrEmpty(accessToken)
                    && (path.StartsWithSegments("/hubs")
                        || path.StartsWithSegments("/api/crypto/content/plain-media")))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<LiberationFleet.Server.Filters.LibraryAccessFilter>();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddProblemDetails();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowConfiguredOrigins", policy =>
        {
            policy.WithOrigins(corsOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .WithExposedHeaders(
                    "X-LF-Nonce",
                    "X-LF-KeyVersion",
                    "X-LF-ResourceId",
                    "Accept-Ranges",
                    "Content-Range",
                    "Content-Length",
                    "Content-Type");
        });
    });
}

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

var app = builder.Build();

if (corsOrigins.Length > 0)
{
    app.UseCors("AllowConfiguredOrigins");
}

app.UseExceptionHandler();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapStaticAssets();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsEnvironment("Docker"))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
// Liveness for Azure App Service / Docker before (and while) migrations run.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<VoiceHub>("/hubs/voice");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapFallbackToFile("/index.html");

// Listen before migrations so platform health probes are not blocked by SQL retries.
await app.StartAsync();
await ApplyMigrationsAsync(app);
await app.WaitForShutdownAsync();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigrations");

    const int maxAttempts = 15;
    Exception? lastError = null;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully");
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            lastError = ex;
            logger.LogWarning(ex, "Database not ready (attempt {Attempt}/{MaxAttempts}). Retrying in 5 seconds...", attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            lastError = ex;
        }
    }

    logger.LogCritical(lastError, "Database migrations failed after {MaxAttempts} attempts", maxAttempts);
    throw new InvalidOperationException($"Database migrations failed after {maxAttempts} attempts.", lastError);
}
