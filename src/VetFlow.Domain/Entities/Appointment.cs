using VetFlow.Domain.Common;
using VetFlow.Domain.Enums;

namespace VetFlow.Domain.Entities;

/// <summary>
/// A booked slot in a veterinarian's calendar. Times are stored in clinic local time.
/// Status workflow: Scheduled → Checked in → In progress → Completed, or Cancelled / No-show.
/// </summary>
public class Appointment
{
    public int Id { get; set; }

    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public int VeterinarianId { get; set; }
    public Veterinarian Veterinarian { get; set; } = null!;

    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }

    public AppointmentType Type { get; set; }
    public string? Reason { get; set; }
    public AppointmentStatus Status { get; private set; } = AppointmentStatus.Scheduled;

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? CheckedInAt { get; private set; }
    public string? CancellationReason { get; private set; }

    /// <summary>The medical record started from this appointment.</summary>
    public int? VisitId { get; set; }

    public TimeSpan Duration => EndsAt - StartsAt;

    /// <summary>Cancelled and no-show appointments free their slot.</summary>
    public bool BlocksCalendar => Status is not (AppointmentStatus.Cancelled or AppointmentStatus.NoShow);

    public bool OverlapsWith(DateTime start, DateTime end) => StartsAt < end && start < EndsAt;

    public void SetTime(DateTime start, int durationMinutes)
    {
        if (durationMinutes <= 0)
        {
            throw new DomainException("Duration must be positive.");
        }

        if (Status != AppointmentStatus.Scheduled && Id != 0)
        {
            throw new DomainException("Only scheduled appointments can be moved.");
        }

        StartsAt = start;
        EndsAt = start.AddMinutes(durationMinutes);
    }

    public void CheckIn(DateTime now)
    {
        Ensure(AppointmentStatus.Scheduled, "check in");
        Status = AppointmentStatus.CheckedIn;
        CheckedInAt = now;
    }

    public void Start()
    {
        if (Status is not (AppointmentStatus.Scheduled or AppointmentStatus.CheckedIn))
        {
            throw new DomainException($"An appointment that is '{Status}' cannot be started.");
        }

        Status = AppointmentStatus.InProgress;
    }

    public void Complete()
    {
        Ensure(AppointmentStatus.InProgress, "complete");
        Status = AppointmentStatus.Completed;
    }

    public void Cancel(string? reason)
    {
        if (Status is not (AppointmentStatus.Scheduled or AppointmentStatus.CheckedIn))
        {
            throw new DomainException($"An appointment that is '{Status}' cannot be cancelled.");
        }

        Status = AppointmentStatus.Cancelled;
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public void MarkNoShow(DateTime now)
    {
        Ensure(AppointmentStatus.Scheduled, "mark as no-show");
        if (now < StartsAt)
        {
            throw new DomainException("An appointment can only be marked as no-show after its start time.");
        }

        Status = AppointmentStatus.NoShow;
    }

    private void Ensure(AppointmentStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new DomainException($"Cannot {action}: the appointment is '{Status}'.");
        }
    }
}
