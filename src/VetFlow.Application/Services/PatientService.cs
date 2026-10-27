using Microsoft.EntityFrameworkCore;
using VetFlow.Application.Common;
using VetFlow.Application.Interfaces;
using VetFlow.Domain.Common;
using VetFlow.Domain.Entities;
using VetFlow.Domain.Enums;

namespace VetFlow.Application.Services;

public sealed record PatientInput(
    int OwnerId, string Name, Species Species, string? Breed, Sex Sex, bool IsNeutered, DateTime? DateOfBirth,
    string? MicrochipNumber, string? Color, string? Allergies, bool IsDeceased);

public sealed class PatientListItem
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public Species Species { get; init; }
    public string? Breed { get; init; }
    public Sex Sex { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public string? MicrochipNumber { get; init; }
    public bool IsDeceased { get; init; }
    public bool HasAllergies { get; init; }
    public int OwnerId { get; init; }
    public string OwnerName { get; init; } = string.Empty;
    public string OwnerPhone { get; init; } = string.Empty;
    public DateTime? LastVisit { get; init; }
}

public sealed record PatientSearchHit(int Id, string Name, string Species, string? Breed, string OwnerName, string OwnerPhone);

public sealed class PatientRecord
{
    public required Patient Patient { get; init; }
    public IReadOnlyList<Visit> Visits { get; init; } = [];
    public IReadOnlyList<Vaccination> Vaccinations { get; init; } = [];
    public IReadOnlyList<Appointment> UpcomingAppointments { get; init; } = [];

    /// <summary>Latest vaccination per vaccine type – this decides whether the patient is protected.</summary>
    public IReadOnlyList<Vaccination> CurrentVaccinations =>
        Vaccinations.GroupBy(v => v.VaccineTypeId)
            .Select(g => g.OrderByDescending(v => v.AdministeredOn).First())
            .OrderBy(v => v.NextDueOn)
            .ToList();

    /// <summary>Weight curve from closed and open visits (oldest first).</summary>
    public IReadOnlyList<(DateTime Date, decimal Kg)> WeightHistory =>
        Visits.Where(v => v.WeightKg.HasValue)
            .OrderBy(v => v.StartedAt)
            .Select(v => (v.StartedAt, v.WeightKg!.Value))
            .ToList();
}

public interface IPatientService
{
    Task<PagedResult<PatientListItem>> SearchAsync(string? query, Species? species, int page, CancellationToken ct = default);
    Task<IReadOnlyList<PatientSearchHit>> QuickSearchAsync(string term, CancellationToken ct = default);
    Task<Patient> GetAsync(int id, CancellationToken ct = default);
    Task<PatientRecord> GetRecordAsync(int id, CancellationToken ct = default);
    Task<bool> MicrochipExistsAsync(string microchip, int? excludeId, CancellationToken ct = default);
    Task<Patient> CreateAsync(PatientInput input, CancellationToken ct = default);
    Task UpdateAsync(int id, PatientInput input, CancellationToken ct = default);
}

public sealed class PatientService(IApplicationDbContext db, IClinicClock clock) : IPatientService
{
    public Task<PagedResult<PatientListItem>> SearchAsync(string? query, Species? species, int page, CancellationToken ct = default)
    {
        var patients = db.Patients.AsNoTracking();

        if (species.HasValue)
        {
            patients = patients.Where(p => p.Species == species.Value);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            patients = patients.Where(p =>
                p.Name.Contains(term) || (p.Breed != null && p.Breed.Contains(term)) || p.MicrochipNumber == term
                || p.Owner.FirstName.Contains(term) || p.Owner.LastName.Contains(term));
        }

        var projected = patients
            .OrderBy(p => p.Name)
            .Select(p => new PatientListItem
            {
                Id = p.Id,
                Name = p.Name,
                Species = p.Species,
                Breed = p.Breed,
                Sex = p.Sex,
                DateOfBirth = p.DateOfBirth,
                MicrochipNumber = p.MicrochipNumber,
                IsDeceased = p.IsDeceased,
                HasAllergies = p.Allergies != null && p.Allergies != "",
                OwnerId = p.OwnerId,
                OwnerName = p.Owner.FirstName + " " + p.Owner.LastName,
                OwnerPhone = p.Owner.Phone,
                LastVisit = p.Visits.Max(v => (DateTime?)v.StartedAt)
            });

        return PagedResult<PatientListItem>.CreateAsync(projected, page, 20, ct);
    }

    public async Task<IReadOnlyList<PatientSearchHit>> QuickSearchAsync(string term, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
        {
            return [];
        }

        term = term.Trim();
        var rows = await db.Patients.AsNoTracking()
            .Where(p => !p.IsDeceased && (p.Name.Contains(term) || p.Owner.LastName.Contains(term)
                                          || p.Owner.FirstName.Contains(term) || p.MicrochipNumber == term))
            .OrderBy(p => p.Name)
            .Take(10)
            .Select(p => new { p.Id, p.Name, p.Species, p.Breed, Owner = p.Owner.FirstName + " " + p.Owner.LastName, p.Owner.Phone })
            .ToListAsync(ct);

        return rows.Select(r => new PatientSearchHit(r.Id, r.Name, r.Species.ToString(), r.Breed, r.Owner, TurkishPhone.Format(r.Phone)))
            .ToList();
    }

    public async Task<Patient> GetAsync(int id, CancellationToken ct = default) =>
        await db.Patients.AsNoTracking().Include(p => p.Owner).FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new NotFoundException(nameof(Patient), id);

    public async Task<PatientRecord> GetRecordAsync(int id, CancellationToken ct = default)
    {
        var patient = await GetAsync(id, ct);
        var today = clock.Today;

        var visits = await db.Visits.AsNoTracking()
            .Where(v => v.PatientId == id)
            .Include(v => v.Veterinarian)
            .Include(v => v.Items)
            .OrderByDescending(v => v.StartedAt)
            .ToListAsync(ct);

        var vaccinations = await db.Vaccinations.AsNoTracking()
            .Where(v => v.PatientId == id)
            .Include(v => v.VaccineType)
            .OrderByDescending(v => v.AdministeredOn)
            .ToListAsync(ct);

        var upcoming = await db.Appointments.AsNoTracking()
            .Where(a => a.PatientId == id && a.StartsAt >= today
                        && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow)
            .Include(a => a.Veterinarian)
            .OrderBy(a => a.StartsAt)
            .ToListAsync(ct);

        return new PatientRecord { Patient = patient, Visits = visits, Vaccinations = vaccinations, UpcomingAppointments = upcoming };
    }

    public Task<bool> MicrochipExistsAsync(string microchip, int? excludeId, CancellationToken ct = default)
    {
        var normalized = MicrochipNumber.Normalize(microchip);
        return db.Patients.AnyAsync(p => p.MicrochipNumber == normalized && (excludeId == null || p.Id != excludeId), ct);
    }

    public async Task<Patient> CreateAsync(PatientInput input, CancellationToken ct = default)
    {
        await EnsureOwnerExistsAsync(input.OwnerId, ct);
        var patient = new Patient { CreatedAt = clock.Now };
        Apply(patient, input);
        db.Patients.Add(patient);
        await db.SaveChangesAsync(ct);
        return patient;
    }

    public async Task UpdateAsync(int id, PatientInput input, CancellationToken ct = default)
    {
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException(nameof(Patient), id);
        await EnsureOwnerExistsAsync(input.OwnerId, ct);
        Apply(patient, input);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureOwnerExistsAsync(int ownerId, CancellationToken ct)
    {
        if (!await db.Owners.AnyAsync(o => o.Id == ownerId, ct))
        {
            throw new NotFoundException(nameof(Owner), ownerId);
        }
    }

    private void Apply(Patient patient, PatientInput input)
    {
        if (input.DateOfBirth.HasValue && input.DateOfBirth.Value.Date > clock.Today)
        {
            throw new DomainException("Date of birth cannot be in the future.");
        }

        if (!string.IsNullOrWhiteSpace(input.MicrochipNumber) && !MicrochipNumber.IsValid(input.MicrochipNumber))
        {
            throw new DomainException("Microchip number must have 15 digits.");
        }

        patient.OwnerId = input.OwnerId;
        patient.Name = input.Name.Trim();
        patient.Species = input.Species;
        patient.Breed = Clean(input.Breed);
        patient.Sex = input.Sex;
        patient.IsNeutered = input.IsNeutered;
        patient.DateOfBirth = input.DateOfBirth?.Date;
        patient.MicrochipNumber = string.IsNullOrWhiteSpace(input.MicrochipNumber) ? null : MicrochipNumber.Normalize(input.MicrochipNumber);
        patient.Color = Clean(input.Color);
        patient.Allergies = Clean(input.Allergies);
        patient.IsDeceased = input.IsDeceased;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
