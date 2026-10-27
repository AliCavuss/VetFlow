using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using VetFlow.Application.Services;
using VetFlow.Domain.Common;
using VetFlow.Domain.Entities;
using VetFlow.Domain.Enums;

namespace VetFlow.Web.Models;

public sealed class LoginViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    [Display(Name = "Remember me")] public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public sealed class ErrorViewModel
{
    public string? RequestId { get; set; }
    public int StatusCode { get; set; } = 500;
}

public sealed class OwnerFormModel
{
    public int? Id { get; set; }
    [Required, StringLength(60), Display(Name = "First name")] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(60), Display(Name = "Last name")] public string LastName { get; set; } = string.Empty;
    [Required, TurkishPhone, Display(Name = "Mobile phone")] public string Phone { get; set; } = string.Empty;
    [EmailAddress, StringLength(150)] public string? Email { get; set; }
    [StringLength(300)] public string? Address { get; set; }

    [Display(Name = "Owner agrees to receive SMS reminders (KVKK consent)")]
    public bool ReminderConsent { get; set; } = true;

    [StringLength(1000), DataType(DataType.MultilineText)] public string? Notes { get; set; }

    public OwnerInput ToInput() => new(FirstName, LastName, Phone, Email, Address, ReminderConsent, Notes);

    public static OwnerFormModel From(Owner o) => new()
    {
        Id = o.Id, FirstName = o.FirstName, LastName = o.LastName, Phone = TurkishPhone.Format(o.Phone),
        Email = o.Email, Address = o.Address, ReminderConsent = o.ReminderConsent, Notes = o.Notes
    };
}

public sealed class OwnerDetailsViewModel
{
    public required Owner Owner { get; init; }
    public IReadOnlyList<Invoice> Invoices { get; init; } = [];
    public decimal OpenBalance => Invoices.Where(i => i.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid).Sum(i => i.Balance);
}

public sealed class PatientFormModel
{
    public int? Id { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Select an owner.")] public int OwnerId { get; set; }
    [BindNever, ValidateNever] public string? OwnerName { get; set; }
    [Required, StringLength(60)] public string Name { get; set; } = string.Empty;
    public Species Species { get; set; } = Species.Dog;
    [StringLength(60)] public string? Breed { get; set; }
    public Sex Sex { get; set; } = Sex.Unknown;
    [Display(Name = "Neutered / spayed")] public bool IsNeutered { get; set; }
    [DataType(DataType.Date), Display(Name = "Date of birth")] public DateTime? DateOfBirth { get; set; }
    [Microchip, Display(Name = "Microchip number")] public string? MicrochipNumber { get; set; }
    [StringLength(40), Display(Name = "Colour / markings")] public string? Color { get; set; }
    [StringLength(500), Display(Name = "Allergies & intolerances")] public string? Allergies { get; set; }
    [Display(Name = "Deceased")] public bool IsDeceased { get; set; }

    public PatientInput ToInput() => new(OwnerId, Name, Species, Breed, Sex, IsNeutered, DateOfBirth, MicrochipNumber, Color, Allergies, IsDeceased);

    public static PatientFormModel From(Patient p) => new()
    {
        Id = p.Id, OwnerId = p.OwnerId, OwnerName = p.Owner?.FullName, Name = p.Name, Species = p.Species, Breed = p.Breed,
        Sex = p.Sex, IsNeutered = p.IsNeutered, DateOfBirth = p.DateOfBirth, MicrochipNumber = p.MicrochipNumber,
        Color = p.Color, Allergies = p.Allergies, IsDeceased = p.IsDeceased
    };
}

public sealed class AppointmentFormModel
{
    public int? Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a patient.")]
    [Display(Name = "Patient")]
    public int PatientId { get; set; }

    [BindNever, ValidateNever] public string? PatientLabel { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a veterinarian.")]
    [Display(Name = "Veterinarian")]
    public int VeterinarianId { get; set; }

    public AppointmentType Type { get; set; } = AppointmentType.Checkup;

    [Required, DataType(DataType.Date)]
    public DateTime Date { get; set; }

    [Required, RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Pick a start time.")]
    [Display(Name = "Start time")]
    public string Time { get; set; } = string.Empty;

    [Range(5, 480), Display(Name = "Duration (min)")]
    public int DurationMinutes { get; set; } = 30;

    [StringLength(300), Display(Name = "Reason for visit")]
    public string? Reason { get; set; }

    [BindNever, ValidateNever] public IReadOnlyList<SelectListItem> VeterinarianOptions { get; set; } = [];

    public DateTime GetStartsAt() => Date.Date.Add(TimeSpan.Parse(Time, System.Globalization.CultureInfo.InvariantCulture));

    public AppointmentInput ToInput() => new(PatientId, VeterinarianId, Type, GetStartsAt(), DurationMinutes, Reason);

    public static AppointmentFormModel From(Appointment a) => new()
    {
        Id = a.Id, PatientId = a.PatientId, PatientLabel = $"{a.Patient.Name} ({a.Patient.Owner.FullName})",
        VeterinarianId = a.VeterinarianId, Type = a.Type, Date = a.StartsAt.Date, Time = a.StartsAt.ToString("HH:mm"),
        DurationMinutes = (int)a.Duration.TotalMinutes, Reason = a.Reason
    };
}

public sealed class CalendarViewModel
{
    public DateTime WeekStart { get; init; }
    public DateTime Today { get; init; }
    public int? VeterinarianId { get; init; }
    public IReadOnlyList<Veterinarian> Veterinarians { get; init; } = [];
    public IReadOnlyList<Appointment> Appointments { get; init; } = [];
    public IEnumerable<DateTime> Days => Enumerable.Range(0, 6).Select(i => WeekStart.AddDays(i));
    public const int FirstHour = 9;
    public const int LastHour = 19;
    public const int PixelsPerHour = 64;
}

public sealed class VisitNotesFormModel
{
    [Range(0.01, 200), Display(Name = "Weight (kg)")] public decimal? WeightKg { get; set; }
    [Range(30, 45), Display(Name = "Temperature (°C)")] public decimal? TemperatureC { get; set; }
    [Range(10, 400), Display(Name = "Heart rate (bpm)")] public int? HeartRate { get; set; }
    [StringLength(4000), Display(Name = "S – Subjective (history, owner's report)")] public string? Subjective { get; set; }
    [StringLength(4000), Display(Name = "O – Objective (examination findings)")] public string? Objective { get; set; }
    [StringLength(2000), Display(Name = "A – Assessment (diagnosis)")] public string? Assessment { get; set; }
    [StringLength(4000), Display(Name = "P – Plan (treatment, follow-up)")] public string? Plan { get; set; }

    public VisitNotesInput ToInput() => new(WeightKg, TemperatureC, HeartRate, Subjective, Objective, Assessment, Plan);

    public static VisitNotesFormModel From(Visit v) => new()
    {
        WeightKg = v.WeightKg, TemperatureC = v.TemperatureC, HeartRate = v.HeartRate,
        Subjective = v.Subjective, Objective = v.Objective, Assessment = v.Assessment, Plan = v.Plan
    };
}

public sealed class VisitViewModel
{
    public required VisitDetails Details { get; init; }
    public required VisitNotesFormModel Notes { get; init; }
    public IReadOnlyList<Product> Products { get; init; } = [];
    public IReadOnlyList<VaccineType> VaccineTypes { get; init; } = [];
    public Visit Visit => Details.Visit;
}

public sealed class ProductFormModel
{
    public int? Id { get; set; }
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(30), RegularExpression(@"^[A-Za-z0-9\-]+$", ErrorMessage = "Letters, digits and dashes only."), Display(Name = "SKU / code")] public string Sku { get; set; } = string.Empty;
    public ProductCategory Category { get; set; } = ProductCategory.Medication;
    [Required, StringLength(20), Display(Name = "Unit")] public string Unit { get; set; } = "pcs";
    [Range(0, 1_000_000), Display(Name = "Net price (₺)")] public decimal UnitPrice { get; set; }
    [Range(0, 100), Display(Name = "VAT (KDV) %")] public decimal VatPercent { get; set; } = 20;
    [Display(Name = "Track stock")] public bool TracksStock { get; set; } = true;
    [Range(0, 100_000), Display(Name = "Reorder level")] public decimal ReorderLevel { get; set; }
    [Range(0, 100_000), Display(Name = "Opening stock")] public decimal? InitialStock { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;

    public ProductInput ToInput() => new(Name, Sku, Category, Unit, UnitPrice, VatPercent / 100m, TracksStock, ReorderLevel, IsActive, InitialStock);

    public static ProductFormModel From(Product p) => new()
    {
        Id = p.Id, Name = p.Name, Sku = p.Sku, Category = p.Category, Unit = p.Unit, UnitPrice = p.UnitPrice,
        VatPercent = (int)Math.Round(p.VatRate * 100), TracksStock = p.TracksStock, ReorderLevel = p.ReorderLevel, IsActive = p.IsActive
    };
}

public sealed class InvoiceDetailsViewModel
{
    public required Invoice Invoice { get; init; }
    public IReadOnlyList<Product> Products { get; init; } = [];
    public string ClinicName { get; init; } = "VetFlow Veterinary Clinic";
}
