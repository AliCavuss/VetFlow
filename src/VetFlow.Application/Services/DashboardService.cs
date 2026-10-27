using Microsoft.EntityFrameworkCore;
using VetFlow.Application.Interfaces;
using VetFlow.Domain.Entities;
using VetFlow.Domain.Enums;

namespace VetFlow.Application.Services;

public sealed record DailyAmount(DateTime Day, decimal Amount);

public sealed class DashboardSummary
{
    public DateTime Today { get; init; }
    public IReadOnlyList<Appointment> TodayAppointments { get; init; } = [];
    public IReadOnlyList<Visit> OpenVisits { get; init; } = [];
    public int WaitingCount { get; init; }
    public decimal RevenueToday { get; init; }
    public decimal RevenueThisMonth { get; init; }
    public decimal Outstanding { get; init; }
    public int OverdueInvoices { get; init; }
    public IReadOnlyList<Product> LowStock { get; init; } = [];
    public IReadOnlyList<VaccinationDueItem> VaccinationsDue { get; init; } = [];
    public IReadOnlyList<DailyAmount> Revenue30Days { get; init; } = [];
}

public interface IDashboardService
{
    Task<DashboardSummary> GetAsync(int? veterinarianId, CancellationToken ct = default);
}

public sealed class DashboardService(
    IApplicationDbContext db,
    IAppointmentService appointments,
    IVisitService visits,
    IInventoryService inventory,
    IVaccinationService vaccinations,
    IClinicClock clock) : IDashboardService
{
    public async Task<DashboardSummary> GetAsync(int? veterinarianId, CancellationToken ct = default)
    {
        var today = clock.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var from30 = today.AddDays(-29);

        var todays = await appointments.GetRangeAsync(today, today.AddDays(1), veterinarianId, ct);

        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.PaidAt >= (from30 < monthStart ? from30 : monthStart))
            .Select(p => new { p.PaidAt, p.Amount })
            .ToListAsync(ct);

        var daily = Enumerable.Range(0, 30)
            .Select(i => from30.AddDays(i))
            .Select(day => new DailyAmount(day, payments.Where(p => p.PaidAt.Date == day).Sum(p => p.Amount)))
            .ToList();

        var unpaid = await db.Invoices.AsNoTracking()
            .Include(i => i.Lines).Include(i => i.Payments)
            .Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
            .ToListAsync(ct);

        return new DashboardSummary
        {
            Today = today,
            TodayAppointments = todays,
            WaitingCount = todays.Count(a => a.Status == AppointmentStatus.CheckedIn),
            OpenVisits = await visits.GetOpenAsync(ct),
            RevenueToday = payments.Where(p => p.PaidAt.Date == today).Sum(p => p.Amount),
            RevenueThisMonth = payments.Where(p => p.PaidAt >= monthStart).Sum(p => p.Amount),
            Outstanding = unpaid.Sum(i => i.Balance),
            OverdueInvoices = unpaid.Count(i => i.IsOverdue(today)),
            LowStock = await inventory.GetLowStockAsync(ct),
            VaccinationsDue = (await vaccinations.GetDueAsync(14, ct)).Take(8).ToList(),
            Revenue30Days = daily
        };
    }
}
