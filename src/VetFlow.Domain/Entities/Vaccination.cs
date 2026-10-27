using VetFlow.Domain.Common;
using VetFlow.Domain.Enums;

namespace VetFlow.Domain.Entities;

/// <summary>A vaccine in the clinic's protocol, e.g. "Rabies – every 365 days".</summary>
public class VaccineType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Species the vaccine is for; <c>null</c> = any species.</summary>
    public Species? Species { get; set; }

    /// <summary>Days until the booster is due.</summary>
    public int IntervalDays { get; set; } = 365;

    /// <summary>Stock item billed and deducted when the vaccine is administered.</summary>
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
}

public class Vaccination
{
    public const int DueSoonDays = 30;

    public int Id { get; set; }

    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public int VaccineTypeId { get; set; }
    public VaccineType VaccineType { get; set; } = null!;

    public DateTime AdministeredOn { get; set; }
    public DateTime NextDueOn { get; set; }
    public string? BatchNumber { get; set; }
    public int? VisitId { get; set; }
    public string AdministeredBy { get; set; } = string.Empty;

    public static Vaccination Record(Patient patient, VaccineType type, DateTime date, string administeredBy,
        string? batchNumber = null, int? visitId = null)
    {
        if (type.Species.HasValue && type.Species != patient.Species)
        {
            throw new DomainException($"{type.Name} is a {type.Species} vaccine and cannot be given to a {patient.Species}.");
        }

        return new Vaccination
        {
            PatientId = patient.Id,
            VaccineTypeId = type.Id,
            AdministeredOn = date.Date,
            NextDueOn = date.Date.AddDays(type.IntervalDays),
            BatchNumber = string.IsNullOrWhiteSpace(batchNumber) ? null : batchNumber.Trim(),
            VisitId = visitId,
            AdministeredBy = administeredBy
        };
    }

    public VaccinationDueStatus DueStatus(DateTime today) =>
        NextDueOn.Date < today.Date ? VaccinationDueStatus.Overdue
        : NextDueOn.Date <= today.Date.AddDays(DueSoonDays) ? VaccinationDueStatus.DueSoon
        : VaccinationDueStatus.UpToDate;
}
