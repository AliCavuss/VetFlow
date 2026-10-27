using VetFlow.Domain.Entities;

namespace VetFlow.Domain.Services;

/// <summary>Opening hours of the clinic. Appointments must start and end within them.</summary>
public sealed record ClinicSchedule(TimeOnly Opens, TimeOnly Closes, int SlotMinutes, IReadOnlySet<DayOfWeek> ClosedDays)
{
    public static ClinicSchedule Default { get; } =
        new(new TimeOnly(9, 0), new TimeOnly(19, 0), 15, new HashSet<DayOfWeek> { DayOfWeek.Sunday });

    public bool IsOpenOn(DateTime day) => !ClosedDays.Contains(day.DayOfWeek);
}

/// <summary>
/// Pure booking rules (no database): opening hours, closed days, no bookings in the past and
/// no double-booking of a veterinarian. Used both for validation and for the free-slot picker.
/// </summary>
public static class AppointmentScheduler
{
    public static IReadOnlyList<string> Validate(
        DateTime start,
        int durationMinutes,
        IEnumerable<Appointment> veterinarianAppointments,
        ClinicSchedule schedule,
        DateTime now,
        int? ignoreAppointmentId = null)
    {
        var errors = new List<string>();
        var end = start.AddMinutes(durationMinutes);

        if (durationMinutes is < 5 or > 480)
        {
            errors.Add("Duration must be between 5 minutes and 8 hours.");
            return errors;
        }

        if (start < now)
        {
            errors.Add("Appointments cannot be booked in the past.");
        }

        if (!schedule.IsOpenOn(start))
        {
            errors.Add($"The clinic is closed on {start.DayOfWeek}s.");
        }
        else if (TimeOnly.FromDateTime(start) < schedule.Opens
                 || end.Date != start.Date
                 || TimeOnly.FromDateTime(end) > schedule.Closes)
        {
            errors.Add($"Appointments must be between {schedule.Opens:HH\\:mm} and {schedule.Closes:HH\\:mm}.");
        }

        var clash = veterinarianAppointments
            .Where(a => a.Id != ignoreAppointmentId && a.BlocksCalendar)
            .OrderBy(a => a.StartsAt)
            .FirstOrDefault(a => a.OverlapsWith(start, end));

        if (clash is not null)
        {
            errors.Add($"The veterinarian already has an appointment from {clash.StartsAt:HH\\:mm} to {clash.EndsAt:HH\\:mm}.");
        }

        return errors;
    }

    /// <summary>Start times on <paramref name="day"/> where an appointment of the given length fits.</summary>
    public static IReadOnlyList<DateTime> AvailableSlots(
        DateTime day,
        int durationMinutes,
        IEnumerable<Appointment> veterinarianAppointments,
        ClinicSchedule schedule,
        DateTime now)
    {
        if (!schedule.IsOpenOn(day) || durationMinutes <= 0)
        {
            return [];
        }

        var booked = veterinarianAppointments.Where(a => a.BlocksCalendar).ToList();
        var slots = new List<DateTime>();
        var cursor = day.Date.Add(schedule.Opens.ToTimeSpan());
        var closes = day.Date.Add(schedule.Closes.ToTimeSpan());

        for (; cursor.AddMinutes(durationMinutes) <= closes; cursor = cursor.AddMinutes(schedule.SlotMinutes))
        {
            var end = cursor.AddMinutes(durationMinutes);
            if (cursor >= now && !booked.Any(a => a.OverlapsWith(cursor, end)))
            {
                slots.Add(cursor);
            }
        }

        return slots;
    }
}
