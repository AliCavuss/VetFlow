using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VetFlow.Domain.Common;
using VetFlow.Domain.Entities;
using VetFlow.Domain.Enums;
using VetFlow.Application.Interfaces;

namespace VetFlow.Application.Services;

public sealed record ReminderRunResult(int Created, int Sent, int Failed);

public interface IReminderService
{
    /// <summary>Queues SMS reminders for boosters due within 7 days and tomorrow's appointments, then sends them.</summary>
    Task<ReminderRunResult> RunAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Reminder>> GetRecentAsync(int take = 50, CancellationToken ct = default);
}

public sealed class ReminderService(
    IApplicationDbContext db,
    IVaccinationService vaccinations,
    ISmsSender sms,
    IClinicClock clock,
    ILogger<ReminderService> logger) : IReminderService
{
    public const int VaccinationLeadDays = 7;

    public async Task<ReminderRunResult> RunAsync(CancellationToken ct = default)
    {
        var now = clock.Now;
        var created = 0;

        // 1) Boosters due soon – only owners who gave consent (KVKK).
        var due = (await vaccinations.GetDueAsync(VaccinationLeadDays, ct))
            .Where(d => d.ReminderConsent && d.NextDueOn >= clock.Today)
            .ToList();
        foreach (var item in due)
        {
            var key = $"vaccination:{item.VaccinationId}";
            if (await db.Reminders.AnyAsync(r => r.DedupKey == key, ct)) continue;

            db.Reminders.Add(new Reminder
            {
                OwnerId = item.OwnerId, PatientId = item.PatientId, Type = ReminderType.VaccinationDue,
                Recipient = item.OwnerPhone, DedupKey = key, CreatedAt = now,
                Message = $"VetFlow Clinic: {item.PatientName}'s {item.VaccineName} booster is due on {item.NextDueOn:dd.MM.yyyy}. Reply or call us to book an appointment."
            });
            created++;
        }

        // 2) Appointments tomorrow.
        var tomorrow = clock.Today.AddDays(1);
        var dayAfter = tomorrow.AddDays(1);
        var appointments = await db.Appointments.AsNoTracking()
            .Include(a => a.Patient).ThenInclude(p => p.Owner)
            .Include(a => a.Veterinarian)
            .Where(a => a.StartsAt >= tomorrow && a.StartsAt < dayAfter && a.Status == AppointmentStatus.Scheduled
                        && a.Patient.Owner.ReminderConsent)
            .ToListAsync(ct);
        foreach (var a in appointments)
        {
            var key = $"appointment:{a.Id}";
            if (await db.Reminders.AnyAsync(r => r.DedupKey == key, ct)) continue;

            db.Reminders.Add(new Reminder
            {
                OwnerId = a.Patient.OwnerId, PatientId = a.PatientId, Type = ReminderType.AppointmentTomorrow,
                Recipient = a.Patient.Owner.Phone, DedupKey = key, CreatedAt = now,
                Message = $"VetFlow Clinic: reminder – {a.Patient.Name} has an appointment with {a.Veterinarian.DisplayName} tomorrow at {a.StartsAt:HH\\:mm}."
            });
            created++;
        }

        await db.SaveChangesAsync(ct);

        // 3) Send everything that is still pending (also retries earlier failures).
        int sent = 0, failed = 0;
        var pending = await db.Reminders.Where(r => r.Status != ReminderStatus.Sent).OrderBy(r => r.CreatedAt).Take(200).ToListAsync(ct);
        foreach (var reminder in pending)
        {
            try
            {
                await sms.SendAsync(reminder.Recipient, reminder.Message, ct);
                reminder.Status = ReminderStatus.Sent;
                reminder.SentAt = clock.Now;
                reminder.Error = null;
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Sending reminder {ReminderId} to {Phone} failed", reminder.Id, reminder.Recipient);
                reminder.Status = ReminderStatus.Failed;
                reminder.Error = ex.Message.Length > 300 ? ex.Message[..300] : ex.Message;
                failed++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new ReminderRunResult(created, sent, failed);
    }

    public async Task<IReadOnlyList<Reminder>> GetRecentAsync(int take = 50, CancellationToken ct = default) =>
        await db.Reminders.AsNoTracking()
            .Include(r => r.Owner)
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
}
