using Microsoft.EntityFrameworkCore;
using VetFlow.Application.Interfaces;
using VetFlow.Domain.Common;
using VetFlow.Domain.Entities;
using VetFlow.Domain.Enums;

namespace VetFlow.Application.Services;

public sealed record VisitNotesInput(
    decimal? WeightKg, decimal? TemperatureC, int? HeartRate,
    string? Subjective, string? Objective, string? Assessment, string? Plan);

public sealed class VisitDetails
{
    public required Visit Visit { get; init; }
    public IReadOnlyList<Vaccination> Vaccinations { get; init; } = [];
    public int? InvoiceId { get; init; }
}

public interface IVisitService
{
    Task<VisitDetails> GetAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Visit>> GetOpenAsync(CancellationToken ct = default);
    Task<int> StartWalkInAsync(int patientId, int veterinarianId, CancellationToken ct = default);
    Task SaveNotesAsync(int id, VisitNotesInput input, CancellationToken ct = default);
    Task AddItemAsync(int visitId, int productId, decimal quantity, string user, CancellationToken ct = default);
    Task RemoveItemAsync(int visitId, int itemId, string user, CancellationToken ct = default);
    Task RecordVaccinationAsync(int visitId, int vaccineTypeId, string? batchNumber, string user, CancellationToken ct = default);

    /// <summary>Closes the medical record, completes the appointment and creates a draft invoice. Returns the invoice id.</summary>
    Task<int?> CloseAsync(int id, CancellationToken ct = default);
}

public sealed class VisitService(IApplicationDbContext db, IClinicClock clock) : IVisitService
{
    public async Task<VisitDetails> GetAsync(int id, CancellationToken ct = default)
    {
        var visit = await db.Visits.AsNoTracking()
                        .Include(v => v.Patient).ThenInclude(p => p.Owner)
                        .Include(v => v.Veterinarian)
                        .Include(v => v.Items).ThenInclude(i => i.Product)
                        .FirstOrDefaultAsync(v => v.Id == id, ct)
                    ?? throw new NotFoundException(nameof(Visit), id);

        var vaccinations = await db.Vaccinations.AsNoTracking()
            .Include(v => v.VaccineType)
            .Where(v => v.VisitId == id)
            .ToListAsync(ct);

        var invoiceId = await db.Invoices.Where(i => i.VisitId == id).Select(i => (int?)i.Id).FirstOrDefaultAsync(ct);

        return new VisitDetails { Visit = visit, Vaccinations = vaccinations, InvoiceId = invoiceId };
    }

    public async Task<IReadOnlyList<Visit>> GetOpenAsync(CancellationToken ct = default) =>
        await db.Visits.AsNoTracking()
            .Include(v => v.Patient)
            .Include(v => v.Veterinarian)
            .Where(v => v.Status == VisitStatus.Open)
            .OrderBy(v => v.StartedAt)
            .ToListAsync(ct);

    public async Task<int> StartWalkInAsync(int patientId, int veterinarianId, CancellationToken ct = default)
    {
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct) ?? throw new NotFoundException(nameof(Patient), patientId);
        if (patient.IsDeceased) throw new DomainException($"{patient.Name} is marked as deceased.");
        if (!await db.Veterinarians.AnyAsync(v => v.Id == veterinarianId && v.IsActive, ct))
        {
            throw new DomainException("Select an active veterinarian.");
        }

        var visit = new Visit { PatientId = patientId, VeterinarianId = veterinarianId, StartedAt = clock.Now };
        db.Visits.Add(visit);
        await db.SaveChangesAsync(ct);
        return visit.Id;
    }

    public async Task SaveNotesAsync(int id, VisitNotesInput input, CancellationToken ct = default)
    {
        var visit = await LoadAsync(id, ct);
        visit.EnsureOpen();

        visit.WeightKg = input.WeightKg;
        visit.TemperatureC = input.TemperatureC;
        visit.HeartRate = input.HeartRate;
        visit.Subjective = Clean(input.Subjective);
        visit.Objective = Clean(input.Objective);
        visit.Assessment = Clean(input.Assessment);
        visit.Plan = Clean(input.Plan);

        if (input.WeightKg.HasValue)
        {
            var patient = await db.Patients.FirstAsync(p => p.Id == visit.PatientId, ct);
            patient.LastWeightKg = input.WeightKg;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task AddItemAsync(int visitId, int productId, decimal quantity, string user, CancellationToken ct = default)
    {
        var visit = await LoadAsync(visitId, ct);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct) ?? throw new NotFoundException(nameof(Product), productId);

        AddItem(visit, product, quantity, user);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveItemAsync(int visitId, int itemId, string user, CancellationToken ct = default)
    {
        var visit = await LoadAsync(visitId, ct);
        var item = visit.Items.FirstOrDefault(i => i.Id == itemId) ?? throw new NotFoundException("Visit item", itemId);
        var product = await db.Products.FirstAsync(p => p.Id == item.ProductId, ct);

        visit.RemoveItem(item);
        db.VisitItems.Remove(item);

        if (product.TracksStock)
        {
            product.AddStock(item.Quantity);
            db.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id, Type = StockMovementType.UsageReversed, Quantity = item.Quantity,
                Reference = $"Visit #{visit.Id}", CreatedAt = clock.Now, CreatedBy = user
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task RecordVaccinationAsync(int visitId, int vaccineTypeId, string? batchNumber, string user, CancellationToken ct = default)
    {
        var visit = await LoadAsync(visitId, ct);
        visit.EnsureOpen();

        var patient = await db.Patients.FirstAsync(p => p.Id == visit.PatientId, ct);
        var type = await db.VaccineTypes.Include(t => t.Product).FirstOrDefaultAsync(t => t.Id == vaccineTypeId, ct)
                   ?? throw new NotFoundException("Vaccine", vaccineTypeId);

        var vaccination = Vaccination.Record(patient, type, visit.StartedAt, user, batchNumber, visit.Id);
        db.Vaccinations.Add(vaccination);

        if (type.Product is not null)
        {
            AddItem(visit, type.Product, 1, user);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<int?> CloseAsync(int id, CancellationToken ct = default)
    {
        var visit = await LoadAsync(id, ct);
        var now = clock.Now;
        visit.Close(now);

        if (visit.AppointmentId.HasValue)
        {
            var appointment = await db.Appointments.FirstOrDefaultAsync(a => a.Id == visit.AppointmentId.Value, ct);
            if (appointment is { Status: AppointmentStatus.InProgress })
            {
                appointment.Complete();
            }
        }

        Invoice? invoice = null;
        if (visit.Items.Count > 0)
        {
            var ownerId = await db.Patients.Where(p => p.Id == visit.PatientId).Select(p => p.OwnerId).FirstAsync(ct);
            invoice = Invoice.CreateFromVisit(visit, ownerId, now);
            db.Invoices.Add(invoice);
        }

        await db.SaveChangesAsync(ct);
        return invoice?.Id;
    }

    private void AddItem(Visit visit, Product product, decimal quantity, string user)
    {
        // Validate everything before touching state, so a failed add leaves the visit and the stock unchanged.
        visit.EnsureOpen();
        if (!product.IsActive) throw new DomainException($"'{product.Name}' is no longer active.");
        product.RemoveStock(quantity);
        visit.AddItem(product, quantity);

        if (product.TracksStock)
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id, Type = StockMovementType.Usage, Quantity = -quantity,
                Reference = $"Visit #{visit.Id}", CreatedAt = clock.Now, CreatedBy = user
            });
        }
    }

    private async Task<Visit> LoadAsync(int id, CancellationToken ct) =>
        await db.Visits.Include(v => v.Items).FirstOrDefaultAsync(v => v.Id == id, ct)
        ?? throw new NotFoundException(nameof(Visit), id);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
