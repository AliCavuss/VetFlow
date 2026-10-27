using Microsoft.EntityFrameworkCore;
using VetFlow.Application.Interfaces;
using VetFlow.Domain.Entities;
using VetFlow.Domain.Enums;

namespace VetFlow.Application.Services;

public sealed class VaccinationDueItem
{
    public int VaccinationId { get; init; }
    public int PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public Species Species { get; init; }
    public int OwnerId { get; init; }
    public string OwnerName { get; init; } = string.Empty;
    public string OwnerPhone { get; init; } = string.Empty;
    public bool ReminderConsent { get; init; }
    public string VaccineName { get; init; } = string.Empty;
    public DateTime AdministeredOn { get; init; }
    public DateTime NextDueOn { get; init; }
}

public interface IVaccinationService
{
    /// <summary>Latest vaccination per patient and vaccine whose booster is due within <paramref name="daysAhead"/> days (or overdue).</summary>
    Task<IReadOnlyList<VaccinationDueItem>> GetDueAsync(int daysAhead, CancellationToken ct = default);

    Task<IReadOnlyList<VaccineType>> GetVaccineTypesAsync(Species? species = null, CancellationToken ct = default);
}

public sealed class VaccinationService(IApplicationDbContext db, IClinicClock clock) : IVaccinationService
{
    public async Task<IReadOnlyList<VaccinationDueItem>> GetDueAsync(int daysAhead, CancellationToken ct = default)
    {
        var until = clock.Today.AddDays(daysAhead);

        return await db.Vaccinations.AsNoTracking()
            .Where(v => v.NextDueOn <= until && !v.Patient.IsDeceased
                        && !db.Vaccinations.Any(newer => newer.PatientId == v.PatientId
                                                         && newer.VaccineTypeId == v.VaccineTypeId
                                                         && newer.AdministeredOn > v.AdministeredOn))
            .OrderBy(v => v.NextDueOn)
            .Select(v => new VaccinationDueItem
            {
                VaccinationId = v.Id,
                PatientId = v.PatientId,
                PatientName = v.Patient.Name,
                Species = v.Patient.Species,
                OwnerId = v.Patient.OwnerId,
                OwnerName = v.Patient.Owner.FirstName + " " + v.Patient.Owner.LastName,
                OwnerPhone = v.Patient.Owner.Phone,
                ReminderConsent = v.Patient.Owner.ReminderConsent,
                VaccineName = v.VaccineType.Name,
                AdministeredOn = v.AdministeredOn,
                NextDueOn = v.NextDueOn
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<VaccineType>> GetVaccineTypesAsync(Species? species = null, CancellationToken ct = default)
    {
        var types = db.VaccineTypes.AsNoTracking().Include(t => t.Product).AsQueryable();
        if (species.HasValue)
        {
            types = types.Where(t => t.Species == null || t.Species == species.Value);
        }

        return await types.OrderBy(t => t.Name).ToListAsync(ct);
    }
}
