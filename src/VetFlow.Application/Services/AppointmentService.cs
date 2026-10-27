using Microsoft.EntityFrameworkCore;
using VetFlow.Application.Interfaces;
using VetFlow.Domain.Common;
using VetFlow.Domain.Entities;
using VetFlow.Domain.Enums;
using VetFlow.Domain.Services;

namespace VetFlow.Application.Services;

public sealed record AppointmentInput(
    int PatientId, int VeterinarianId, AppointmentType Type, DateTime StartsAt, int DurationMinutes, string? Reason);

public interface IAppointmentService
{
    Task<IReadOnlyList<Appointment>> GetRangeAsync(DateTime from, DateTime to, int? veterinarianId, CancellationToken ct = default);
    Task<Appointment> GetAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ValidateAsync(AppointmentInput input, int? ignoreAppointmentId, CancellationToken ct = default);
    Task<IReadOnlyList<DateTime>> GetAvailableSlotsAsync(int veterinarianId, DateTime day, int durationMinutes, int? ignoreAppointmentId, CancellationToken ct = default);
    Task<Appointment> CreateAsync(AppointmentInput input, string createdBy, CancellationToken ct = default);
    Task RescheduleAsync(int id, AppointmentInput input, CancellationToken ct = default);
    Task CheckInAsync(int id, CancellationToken ct = default);
    Task CancelAsync(int id, string? reason, CancellationToken ct = default);
    Task MarkNoShowAsync(int id, CancellationToken ct = default);

    /// <summary>Starts the examination: opens a medical record (visit) for the appointment and returns its id.</summary>
    Task<int> StartVisitAsync(int id, CancellationToken ct = default);
}

public sealed class AppointmentService(IApplicationDbContext db, IClinicClock clock) : IAppointmentService
{
    private static readonly ClinicSchedule Schedule = ClinicSchedule.Default;

    public async Task<IReadOnlyList<Appointment>> GetRangeAsync(DateTime from, DateTime to, int? veterinarianId, CancellationToken ct = default)
    {
        var query = db.Appointments.AsNoTracking()
            .Include(a => a.Patient).ThenInclude(p => p.Owner)
            .Include(a => a.Veterinarian)
            .Where(a => a.StartsAt >= from && a.StartsAt < to);

        if (veterinarianId.HasValue)
        {
            query = query.Where(a => a.VeterinarianId == veterinarianId.Value);
        }

        return await query.OrderBy(a => a.StartsAt).ToListAsync(ct);
    }

    public async Task<Appointment> GetAsync(int id, CancellationToken ct = default) =>
        await db.Appointments.AsNoTracking()
            .Include(a => a.Patient).ThenInclude(p => p.Owner)
            .Include(a => a.Veterinarian)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
        ?? throw new NotFoundException(nameof(Appointment), id);

    public async Task<IReadOnlyList<string>> ValidateAsync(AppointmentInput input, int? ignoreAppointmentId, CancellationToken ct = default)
    {
        var errors = new List<string>();

        var patient = await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == input.PatientId, ct);
        if (patient is null) errors.Add("Select a patient.");
        else if (patient.IsDeceased) errors.Add($"{patient.Name} is marked as deceased.");

        var vet = await db.Veterinarians.AsNoTracking().FirstOrDefaultAsync(v => v.Id == input.VeterinarianId, ct);
        if (vet is null || !vet.IsActive) errors.Add("Select an active veterinarian.");

        if (errors.Count > 0)
        {
            return errors;
        }

        var dayStart = input.StartsAt.Date;
        var dayEnd = dayStart.AddDays(1);
        var sameDay = await db.Appointments.AsNoTracking()
            .Where(a => a.StartsAt >= dayStart && a.StartsAt < dayEnd
                        && (a.VeterinarianId == input.VeterinarianId || a.PatientId == input.PatientId))
            .ToListAsync(ct);

        errors.AddRange(AppointmentScheduler.Validate(
            input.StartsAt, input.DurationMinutes,
            sameDay.Where(a => a.VeterinarianId == input.VeterinarianId),
            Schedule, clock.Now, ignoreAppointmentId));

        var end = input.StartsAt.AddMinutes(input.DurationMinutes);
        var patientClash = sameDay.FirstOrDefault(a => a.PatientId == input.PatientId && a.VeterinarianId != input.VeterinarianId
                                                       && a.Id != ignoreAppointmentId && a.BlocksCalendar
                                                       && a.OverlapsWith(input.StartsAt, end));
        if (patientClash is not null)
        {
            errors.Add($"{patient!.Name} already has another appointment at {patientClash.StartsAt:HH\\:mm}.");
        }

        return errors;
    }

    public async Task<IReadOnlyList<DateTime>> GetAvailableSlotsAsync(
        int veterinarianId, DateTime day, int durationMinutes, int? ignoreAppointmentId, CancellationToken ct = default)
    {
        var dayStart = day.Date;
        var dayEnd = dayStart.AddDays(1);
        var booked = await db.Appointments.AsNoTracking()
            .Where(a => a.VeterinarianId == veterinarianId && a.StartsAt >= dayStart && a.StartsAt < dayEnd
                        && (ignoreAppointmentId == null || a.Id != ignoreAppointmentId))
            .ToListAsync(ct);

        return AppointmentScheduler.AvailableSlots(dayStart, durationMinutes, booked, Schedule, clock.Now);
    }

    public async Task<Appointment> CreateAsync(AppointmentInput input, string createdBy, CancellationToken ct = default)
    {
        await ThrowIfInvalidAsync(input, null, ct);

        var appointment = new Appointment
        {
            PatientId = input.PatientId,
            VeterinarianId = input.VeterinarianId,
            Type = input.Type,
            Reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim(),
            CreatedAt = clock.Now,
            CreatedBy = createdBy
        };
        appointment.SetTime(input.StartsAt, input.DurationMinutes);

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(ct);
        return appointment;
    }

    public async Task RescheduleAsync(int id, AppointmentInput input, CancellationToken ct = default)
    {
        var appointment = await LoadAsync(id, ct);
        await ThrowIfInvalidAsync(input, id, ct);

        appointment.SetTime(input.StartsAt, input.DurationMinutes);
        appointment.PatientId = input.PatientId;
        appointment.VeterinarianId = input.VeterinarianId;
        appointment.Type = input.Type;
        appointment.Reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim();
        await db.SaveChangesAsync(ct);
    }

    public async Task CheckInAsync(int id, CancellationToken ct = default)
    {
        var appointment = await LoadAsync(id, ct);
        appointment.CheckIn(clock.Now);
        await db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(int id, string? reason, CancellationToken ct = default)
    {
        var appointment = await LoadAsync(id, ct);
        appointment.Cancel(reason);
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkNoShowAsync(int id, CancellationToken ct = default)
    {
        var appointment = await LoadAsync(id, ct);
        appointment.MarkNoShow(clock.Now);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> StartVisitAsync(int id, CancellationToken ct = default)
    {
        var appointment = await LoadAsync(id, ct);
        if (appointment.VisitId.HasValue)
        {
            return appointment.VisitId.Value;
        }

        appointment.Start();
        var visit = new Visit
        {
            PatientId = appointment.PatientId,
            VeterinarianId = appointment.VeterinarianId,
            AppointmentId = appointment.Id,
            StartedAt = clock.Now,
            Subjective = appointment.Reason
        };
        db.Visits.Add(visit);
        await db.SaveChangesAsync(ct);

        appointment.VisitId = visit.Id;
        await db.SaveChangesAsync(ct);
        return visit.Id;
    }

    private async Task<Appointment> LoadAsync(int id, CancellationToken ct) =>
        await db.Appointments.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException(nameof(Appointment), id);

    private async Task ThrowIfInvalidAsync(AppointmentInput input, int? ignoreId, CancellationToken ct)
    {
        var errors = await ValidateAsync(input, ignoreId, ct);
        if (errors.Count > 0)
        {
            throw new DomainException(string.Join(" ", errors));
        }
    }
}
