using VetFlow.Domain.Common;
using VetFlow.Domain.Enums;

namespace VetFlow.Domain.Entities;

/// <summary>An animal treated by the clinic.</summary>
public class Patient
{
    public int Id { get; set; }

    public int OwnerId { get; set; }
    public Owner Owner { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public Species Species { get; set; }
    public string? Breed { get; set; }
    public Sex Sex { get; set; }
    public bool IsNeutered { get; set; }
    public DateTime? DateOfBirth { get; set; }

    /// <summary>15-digit ISO 11784 microchip code.</summary>
    public string? MicrochipNumber { get; set; }

    public string? Color { get; set; }

    /// <summary>Known allergies / drug intolerances – shown as a warning on every clinical screen.</summary>
    public string? Allergies { get; set; }

    /// <summary>Last weight recorded during a visit (kg).</summary>
    public decimal? LastWeightKg { get; set; }

    public bool IsDeceased { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<Visit> Visits { get; set; } = new List<Visit>();
    public ICollection<Vaccination> Vaccinations { get; set; } = new List<Vaccination>();

    public string Age(DateTime today) => PatientAge.Describe(DateOfBirth, today);
}
