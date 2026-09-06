using MeetingMinutesAI.Application.Abstractions.Persistence;
using MeetingMinutesAI.Application.Abstractions.Ai;
using MeetingMinutesAI.Application.Abstractions.Auth;
using MeetingMinutesAI.Application.Abstractions.Storage;
using MeetingMinutesAI.Infrastructure.Ai;
using MeetingMinutesAI.Infrastructure.Auth;
using MeetingMinutesAI.Infrastructure.Persistence;
using MeetingMinutesAI.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeetingMinutesAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is required.");
        }

        services.AddDbContext<MeetingMinutesDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddOptions<AudioStorageOptions>()
            .Bind(configuration.GetSection(AudioStorageOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.RootPath))
                {
                    options.RootPath = AudioStorageOptions.DefaultRootPath();
                }
            })
            .Validate(
                options => options.MaxBytes > 0,
                "AudioStorage:MaxBytes must be positive.")
            .Validate(
                options => IsSafeStorageRoot(options.RootPath),
                "AudioStorage:RootPath must be an absolute or resolvable non-root directory.")
            .ValidateOnStart();
        services.AddOptions<AiServiceOptions>()
            .Bind(configuration.GetSection(AiServiceOptions.SectionName))
            .Validate(
                options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
                    && uri.Scheme is "http" or "https",
                "AIService:BaseUrl must be an absolute HTTP or HTTPS URL.")
            .Validate(
                options => options.TimeoutSeconds > 0,
                "AIService:TimeoutSeconds must be positive.")
            .Validate(
                options => options.MaxResponseBytes > 0,
                "AIService:MaxResponseBytes must be positive.")
            .ValidateOnStart();
        services.AddSingleton<IAudioStorage, LocalAudioStorage>();
        services.AddHttpClient<IAiServiceClient, AiServiceClient>((provider, client) =>
        {
            var options = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<AiServiceOptions>>()
                .Value;
            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.SecretKey) && options.SecretKey.Length >= 32,
                "Jwt:SecretKey must be at least 32 characters long.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Issuer),
                "Jwt:Issuer is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Audience),
                "Jwt:Audience is required.")
            .Validate(
                options => options.ExpiryMinutes > 0,
                "Jwt:ExpiryMinutes must be positive.")
            .ValidateOnStart();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IMeetingRepository, MeetingRepository>();
        services.AddScoped<IUnitOfWork>(provider =>
            provider.GetRequiredService<MeetingMinutesDbContext>());

        return services;
    }

    private static bool IsSafeStorageRoot(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(value);
            return !string.Equals(
                fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetPathRoot(fullPath)?.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}
