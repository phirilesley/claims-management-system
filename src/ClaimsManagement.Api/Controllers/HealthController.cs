using ClaimsManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClaimsManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<HealthController> _logger;

    public HealthController(ApplicationDbContext db, ILogger<HealthController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<HealthCheckResponse>> Get(CancellationToken cancellationToken = default)
    {
        var response = new HealthCheckResponse
        {
            Status = "Healthy",
            Timestamp = DateTimeOffset.UtcNow,
            Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            Checks = new Dictionary<string, HealthCheckResult>()
        };

        try
        {
            // Database connectivity check
            var dbCheck = await CheckDatabaseAsync(cancellationToken);
            response.Checks["database"] = dbCheck;

            // Overall status
            if (dbCheck.Status == "Unhealthy")
            {
                response.Status = "Unhealthy";
            }
            else if (dbCheck.Status == "Degraded")
            {
                response.Status = "Degraded";
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            response.Status = "Unhealthy";
            response.Error = ex.Message;
            return StatusCode(503, response);
        }
    }

    [HttpGet("ready")]
    public async Task<ActionResult> ReadinessCheck(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if database is accessible
            await _db.Database.CanConnectAsync(cancellationToken);
            
            // Check if essential tables exist
            var tenantCount = await _db.Tenants.CountAsync(cancellationToken);
            
            return Ok(new { status = "Ready", timestamp = DateTimeOffset.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed");
            return StatusCode(503, new { status = "Not Ready", error = ex.Message });
        }
    }

    [HttpGet("live")]
    public ActionResult LivenessCheck()
    {
        return Ok(new { status = "Alive", timestamp = DateTimeOffset.UtcNow });
    }

    private async Task<HealthCheckResult> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Test basic connectivity
            await _db.Database.CanConnectAsync(cancellationToken);

            // Test query performance
            await _db.Tenants.Take(1).CountAsync(cancellationToken);

            stopwatch.Stop();

            var data = new Dictionary<string, object>
            {
                ["responseTime"] = stopwatch.ElapsedMilliseconds,
                ["connection"] = "Successful"
            };

            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                return HealthCheckResult.Degraded("Database response time is slow", data: data);
            }

            return HealthCheckResult.Healthy("Database is responding normally", data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed", ex, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });
        }
    }
}

public class HealthCheckResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string Version { get; set; } = string.Empty;
    public Dictionary<string, HealthCheckResult> Checks { get; set; } = new();
    public string? Error { get; set; }
}

public class HealthCheckResult
{
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Dictionary<string, object>? Data { get; set; }
    public string? Exception { get; set; }

    public static HealthCheckResult Healthy(string description, Dictionary<string, object>? data = null)
    {
        return new HealthCheckResult
        {
            Status = "Healthy",
            Description = description,
            Data = data
        };
    }

    public static HealthCheckResult Degraded(string description, Dictionary<string, object>? data = null)
    {
        return new HealthCheckResult
        {
            Status = "Degraded",
            Description = description,
            Data = data
        };
    }

    public static HealthCheckResult Unhealthy(string description, Exception? exception = null, Dictionary<string, object>? data = null)
    {
        return new HealthCheckResult
        {
            Status = "Unhealthy",
            Description = description,
            Exception = exception?.Message,
            Data = data
        };
    }
}
