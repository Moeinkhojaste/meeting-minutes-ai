using MeetingMinutesAI.Api.Configuration;
using MeetingMinutesAI.Api.Errors;
using MeetingMinutesAI.Api.Middleware;
using MeetingMinutesAI.Application.Meetings;
using MeetingMinutesAI.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Features;
using MeetingMinutesAI.Infrastructure.Storage;

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
builder.Services
    .AddControllers(options =>
    {
        options.AllowEmptyInputInBodyModelBinding = true;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
            new BadRequestObjectResult(
                new ApiError(
                    "invalid_request",
                    "The request is invalid.",
                    context.HttpContext.TraceIdentifier));
    });
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SafeExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("ETag");
    });
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IMeetingService, MeetingService>();
builder.Services.AddScoped<IMeetingMediaService, MeetingMediaService>();
var audioLimit = builder.Configuration.GetValue<long?>("AudioStorage:MaxBytes")
    ?? AudioStorageOptions.DefaultMaxBytes;
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = audioLimit + 1_048_576;
});
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors();
app.UseExceptionHandler();
app.MapControllers();
app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready");

app.Run();

public partial class Program;
