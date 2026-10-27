using Microsoft.Extensions.Configuration;
using VetFlow.Application.Interfaces;

namespace VetFlow.Infrastructure;

/// <summary>Clinic local time, independent of the server's time zone (e.g. UTC in a container).</summary>
public sealed class ClinicClock : IClinicClock
{
    private readonly TimeProvider _time;
    private readonly TimeZoneInfo _zone;

    public ClinicClock(TimeProvider time, IConfiguration configuration)
    {
        _time = time;
        var zoneId = configuration["Clinic:TimeZone"] ?? "Europe/Istanbul";
        try
        {
            _zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _zone = TimeZoneInfo.Local;
        }
    }

    public DateTime Now => TimeZoneInfo.ConvertTime(_time.GetUtcNow(), _zone).DateTime;
    public DateTime Today => Now.Date;
}
