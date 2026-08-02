using MeetingMinutesAI.Application.Abstractions.Persistence;
using MeetingMinutesAI.Infrastructure.Persistence;
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
        services.AddScoped<IMeetingRepository, MeetingRepository>();
        services.AddScoped<IUnitOfWork>(provider =>
            provider.GetRequiredService<MeetingMinutesDbContext>());

        return services;
    }
}
