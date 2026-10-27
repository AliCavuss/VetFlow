using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VetFlow.Application.Interfaces;
using VetFlow.Infrastructure.BackgroundJobs;
using VetFlow.Infrastructure.Data;
using VetFlow.Infrastructure.Notifications;

namespace VetFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql
                .EnableRetryOnFailure(maxRetryCount: 3)
                .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IClinicClock, ClinicClock>();
        services.AddSingleton<ISmsSender, LoggingSmsSender>();
        services.AddHostedService<ReminderBackgroundService>();

        return services;
    }
}
