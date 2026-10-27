using Microsoft.Extensions.Logging;
using VetFlow.Application.Interfaces;

namespace VetFlow.Infrastructure.Notifications;

/// <summary>
/// Development SMS gateway: writes the message to the log instead of sending it.
/// Swap for a real provider (e.g. Netgsm, İleti Merkezi, Twilio) by implementing <see cref="ISmsSender"/>.
/// </summary>
public sealed class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : ISmsSender
{
    public Task SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        logger.LogInformation("SMS to {Phone}: {Message}", phoneNumber, message);
        return Task.CompletedTask;
    }
}
