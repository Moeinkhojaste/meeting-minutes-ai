using MeetingMinutesAI.Api.Configuration;
using MeetingMinutesAI.Api.Errors;
using MeetingMinutesAI.Api.Middleware;

DotEnvLoader.LoadWithoutOverwritingEnvironment(
    Path.Combine(Directory.GetCurrentDirectory(), ".env")
);

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SafeExceptionHandler>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.MapControllers();
app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready");

app.Run();

public partial class Program;
