using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VetFlow.Application.Services;

namespace VetFlow.Infrastructure.BackgroundJobs;

/// <summary>Runs the reminder job periodically (default: every 60 minutes, first run shortly after start-up).</summary>
public sealed class ReminderBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ReminderBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Reminders:Enabled", true))
        {
            logger.LogInformation("Reminder job is disabled.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue("Reminders:IntervalMinutes", 60)));

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            using var timer = new PeriodicTimer(interval);
            do
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var reminders = scope.ServiceProvider.GetRequiredService<IReminderService>();
                    var result = await reminders.RunAsync(stoppingToken);
                    logger.LogInformation("Reminder job: {Created} queued, {Sent} sent, {Failed} failed.",
                        result.Created, result.Sent, result.Failed);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Reminder job failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }
}
