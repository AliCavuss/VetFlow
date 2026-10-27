using Microsoft.EntityFrameworkCore;
using VetFlow.Domain.Entities;

namespace VetFlow.Application.Interfaces;

/// <summary>The persistence abstraction used by application services (implemented by EF Core in Infrastructure).</summary>
public interface IApplicationDbContext
{
    DbSet<Owner> Owners { get; }
    DbSet<Patient> Patients { get; }
    DbSet<Veterinarian> Veterinarians { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<Visit> Visits { get; }
    DbSet<VisitItem> VisitItems { get; }
    DbSet<Product> Products { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<VaccineType> VaccineTypes { get; }
    DbSet<Vaccination> Vaccinations { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceLine> InvoiceLines { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Reminder> Reminders { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Current date/time in the clinic's time zone (appointments are stored in clinic local time).</summary>
public interface IClinicClock
{
    DateTime Now { get; }
    DateTime Today => Now.Date;
}

/// <summary>Sends SMS messages (a logging implementation is used in development).</summary>
public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message, CancellationToken ct = default);
}
