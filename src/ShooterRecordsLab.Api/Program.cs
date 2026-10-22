using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShooterRecordsLab.Api.Authentication;
using ShooterRecordsLab.Api.Contracts;
using ShooterRecordsLab.Api.Health;
using ShooterRecordsLab.Api.Middleware;
using ShooterRecordsLab.Application.Abstractions;
using ShooterRecordsLab.Application.Matches;
using ShooterRecordsLab.Application.Players;
using ShooterRecordsLab.Application.Rankings;
using ShooterRecordsLab.Infrastructure.Persistence;
using ShooterRecordsLab.Infrastructure.Repositories;

if (args.Contains("--health-probe", StringComparer.Ordinal))
{
    using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    try
    {
        using var response = await probe.GetAsync("http://127.0.0.1:8080/health/live");
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException)
    {
        Environment.ExitCode = 1;
    }
    catch (OperationCanceledException)
    {
        Environment.ExitCode = 1;
    }

    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new OutcomeConverter());
    options.JsonSerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter());
});
builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
{
    var problem = new ProblemDetails
    {
        Status = 400,
        Title = "Invalid request",
        Detail = "Check required fields, data types and allowed values."
    };
    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    problem.Extensions["code"] = "INVALID_REQUEST";
    return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
});
builder.Services.AddExceptionHandler<RecordsExceptionHandler>();
builder.Services.AddRecordsAuthentication(builder.Environment);
builder.Services.AddSingleton(TimeProvider.System);
var connectionString = builder.Configuration.GetConnectionString("ShooterRecords");
DatabaseOptions.ValidateConnectionString(connectionString);
builder.Services.AddDbContext<ShooterRecordsDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.UseCompatibilityLevel(160).CommandTimeout(30)));
builder.Services.AddScoped<IRecordsRepository, SqlRecordsRepository>();
builder.Services.AddScoped<IRankingRepository, SqlRankingRepository>();
builder.Services.AddScoped<PlayerService>();
builder.Services.AddScoped<MatchService>();
builder.Services.AddScoped<RankingService>();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Detail ??= "The request could not be completed.";
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"], timeout: TimeSpan.FromSeconds(3));
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.None);
builder.Logging.AddFilter("Microsoft.IdentityModel", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

var app = builder.Build();

app.Use(async (context, next) =>
{
    var start = Stopwatch.GetTimestamp();
    try
    {
        await next(context);
    }
    finally
    {
        app.Logger.LogInformation("HTTP {Method} {Route} status {StatusCode} duration {DurationMs}ms trace {TraceId}",
            context.Request.Method, context.GetEndpoint()?.DisplayName ?? "unmatched", context.Response.StatusCode,
            Stopwatch.GetElapsedTime(start).TotalMilliseconds, context.TraceIdentifier);
    }
});
app.UseExceptionHandler(new ExceptionHandlerOptions { SuppressDiagnosticsCallback = _ => true });
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

}

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();

public partial class Program;
