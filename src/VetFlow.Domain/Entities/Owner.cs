namespace VetFlow.Domain.Entities;

/// <summary>A client of the clinic – the person responsible for one or more patients.</summary>
public class Owner
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>E.164 format, e.g. +905321234567.</summary>
    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? Address { get; set; }

    /// <summary>Explicit consent (KVKK) to receive SMS reminders.</summary>
    public bool ReminderConsent { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Patient> Patients { get; set; } = new List<Patient>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public string FullName => $"{FirstName} {LastName}";
}
