using Microsoft.EntityFrameworkCore;
using VetFlow.Application.Interfaces;
using VetFlow.Domain.Enums;

namespace VetFlow.Application.Services;

public sealed record MonthlyAmount(int Year, int Month, decimal Amount)
{
    public string Label => new DateTime(Year, Month, 1).ToString("MMM yy");
}

public sealed record CategoryAmount(string Category, decimal NetAmount);
public sealed record TopItem(string Description, decimal Quantity, decimal NetAmount);
public sealed record AppointmentStat(AppointmentType Type, int Total, int Completed, int NoShows);

public sealed class ClinicReport
{
    public IReadOnlyList<MonthlyAmount> RevenueByMonth { get; init; } = [];
    public IReadOnlyList<CategoryAmount> SalesByCategory { get; init; } = [];
    public IReadOnlyList<TopItem> TopItems { get; init; } = [];
    public IReadOnlyList<AppointmentStat> Appointments { get; init; } = [];
    public int NewPatients { get; init; }
    public decimal NoShowRate { get; init; }
}

public interface IReportService
{
    /// <summary>Revenue for the last 12 months and sales mix / appointment statistics for the last 90 days.</summary>
    Task<ClinicReport> GetAsync(CancellationToken ct = default);
}

public sealed class ReportService(IApplicationDbContext db, IClinicClock clock) : IReportService
{
    public async Task<ClinicReport> GetAsync(CancellationToken ct = default)
    {
        var today = clock.Today;
        var firstMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-11);
        var from90 = today.AddDays(-90);

        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.PaidAt >= firstMonth)
            .Select(p => new { p.PaidAt, p.Amount })
            .ToListAsync(ct);

        var byMonth = Enumerable.Range(0, 12)
            .Select(i => firstMonth.AddMonths(i))
            .Select(m => new MonthlyAmount(m.Year, m.Month,
                payments.Where(p => p.PaidAt.Year == m.Year && p.PaidAt.Month == m.Month).Sum(p => p.Amount)))
            .ToList();

        // Sales mix of issued invoices (net, before discount). Grouping is done in memory to keep the SQL simple.
        var lines = await (
                from line in db.InvoiceLines.AsNoTracking()
                join invoice in db.Invoices on line.InvoiceId equals invoice.Id
                where invoice.IssuedAt >= from90 && invoice.Status != InvoiceStatus.Draft && invoice.Status != InvoiceStatus.Cancelled
                select new { line.Description, line.Quantity, line.UnitPrice, line.ProductId })
            .ToListAsync(ct);

        var categories = await db.Products.AsNoTracking().Select(p => new { p.Id, p.Category }).ToListAsync(ct);
        var categoryOf = categories.ToDictionary(p => p.Id, p => p.Category.ToString());

        var byCategory = lines
            .GroupBy(l => l.ProductId.HasValue && categoryOf.TryGetValue(l.ProductId.Value, out var c) ? c : "Other")
            .Select(g => new CategoryAmount(g.Key, g.Sum(l => Math.Round(l.Quantity * l.UnitPrice, 2))))
            .OrderByDescending(c => c.NetAmount)
            .ToList();

        var top = lines
            .GroupBy(l => l.Description)
            .Select(g => new TopItem(g.Key, g.Sum(l => l.Quantity), g.Sum(l => Math.Round(l.Quantity * l.UnitPrice, 2))))
            .OrderByDescending(t => t.NetAmount)
            .Take(8)
            .ToList();

        var appts = await db.Appointments.AsNoTracking()
            .Where(a => a.StartsAt >= from90 && a.StartsAt < today)
            .Select(a => new { a.Type, a.Status })
            .ToListAsync(ct);

        var stats = appts.GroupBy(a => a.Type)
            .Select(g => new AppointmentStat(g.Key, g.Count(),
                g.Count(a => a.Status == AppointmentStatus.Completed), g.Count(a => a.Status == AppointmentStatus.NoShow)))
            .OrderByDescending(s => s.Total)
            .ToList();

        var relevant = appts.Count(a => a.Status is AppointmentStatus.Completed or AppointmentStatus.NoShow);

        return new ClinicReport
        {
            RevenueByMonth = byMonth,
            SalesByCategory = byCategory,
            TopItems = top,
            Appointments = stats,
            NewPatients = await db.Patients.CountAsync(p => p.CreatedAt >= from90, ct),
            NoShowRate = relevant == 0 ? 0 : appts.Count(a => a.Status == AppointmentStatus.NoShow) / (decimal)relevant
        };
    }
}
