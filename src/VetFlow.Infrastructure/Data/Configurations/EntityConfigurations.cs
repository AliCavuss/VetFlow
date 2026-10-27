using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VetFlow.Domain.Entities;

namespace VetFlow.Infrastructure.Data.Configurations;

internal sealed class OwnerConfiguration : IEntityTypeConfiguration<Owner>
{
    public void Configure(EntityTypeBuilder<Owner> b)
    {
        b.Property(o => o.FirstName).HasMaxLength(60).IsRequired();
        b.Property(o => o.LastName).HasMaxLength(60).IsRequired();
        b.Property(o => o.Phone).HasMaxLength(13).IsRequired();
        b.Property(o => o.Email).HasMaxLength(150);
        b.Property(o => o.Address).HasMaxLength(300);
        b.Property(o => o.Notes).HasMaxLength(1000);
        b.Ignore(o => o.FullName);
        b.HasIndex(o => o.Phone).IsUnique();
        b.HasIndex(o => new { o.LastName, o.FirstName });
    }
}

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> b)
    {
        b.Property(p => p.Name).HasMaxLength(60).IsRequired();
        b.Property(p => p.Breed).HasMaxLength(60);
        b.Property(p => p.Color).HasMaxLength(40);
        b.Property(p => p.Allergies).HasMaxLength(500);
        b.Property(p => p.MicrochipNumber).HasMaxLength(15);
        b.Property(p => p.Species).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.Sex).HasConversion<string>().HasMaxLength(10);
        b.Property(p => p.LastWeightKg).HasPrecision(6, 2);
        b.HasIndex(p => p.MicrochipNumber).IsUnique().HasFilter("[MicrochipNumber] IS NOT NULL");
        b.HasIndex(p => p.Name);

        b.HasOne(p => p.Owner).WithMany(o => o.Patients).HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class VeterinarianConfiguration : IEntityTypeConfiguration<Veterinarian>
{
    public void Configure(EntityTypeBuilder<Veterinarian> b)
    {
        b.Property(v => v.FullName).HasMaxLength(100).IsRequired();
        b.Property(v => v.Title).HasMaxLength(30);
        b.Property(v => v.Specialty).HasMaxLength(100);
        b.Property(v => v.CalendarColor).HasMaxLength(7);
        b.Ignore(v => v.DisplayName);
    }
}

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> b)
    {
        b.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.Reason).HasMaxLength(300);
        b.Property(a => a.CancellationReason).HasMaxLength(300);
        b.Property(a => a.CreatedBy).HasMaxLength(256);
        b.Ignore(a => a.Duration);
        b.Ignore(a => a.BlocksCalendar);

        // Calendar queries: "appointments of vet X between two dates".
        b.HasIndex(a => new { a.VeterinarianId, a.StartsAt });
        b.HasIndex(a => new { a.PatientId, a.StartsAt });

        b.HasOne(a => a.Patient).WithMany(p => p.Appointments).HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(a => a.Veterinarian).WithMany().HasForeignKey(a => a.VeterinarianId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> b)
    {
        b.Property(v => v.Status).HasConversion<string>().HasMaxLength(10);
        b.Property(v => v.WeightKg).HasPrecision(6, 2);
        b.Property(v => v.TemperatureC).HasPrecision(4, 1);
        b.Property(v => v.Subjective).HasMaxLength(4000);
        b.Property(v => v.Objective).HasMaxLength(4000);
        b.Property(v => v.Assessment).HasMaxLength(2000);
        b.Property(v => v.Plan).HasMaxLength(4000);
        b.Ignore(v => v.IsOpen);
        b.Ignore(v => v.NetTotal);
        b.HasIndex(v => new { v.PatientId, v.StartedAt });

        b.HasOne(v => v.Patient).WithMany(p => p.Visits).HasForeignKey(v => v.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(v => v.Veterinarian).WithMany().HasForeignKey(v => v.VeterinarianId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(v => v.Items).WithOne().HasForeignKey(i => i.VisitId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class VisitItemConfiguration : IEntityTypeConfiguration<VisitItem>
{
    public void Configure(EntityTypeBuilder<VisitItem> b)
    {
        b.Property(i => i.Description).HasMaxLength(150).IsRequired();
        b.Property(i => i.Quantity).HasPrecision(10, 3);
        b.Property(i => i.UnitPrice).HasPrecision(18, 2);
        b.Property(i => i.VatRate).HasPrecision(5, 4);
        b.Ignore(i => i.NetAmount);
        b.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.Property(p => p.Name).HasMaxLength(150).IsRequired();
        b.Property(p => p.Sku).HasMaxLength(30).IsRequired();
        b.Property(p => p.Unit).HasMaxLength(20).IsRequired();
        b.Property(p => p.Category).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.UnitPrice).HasPrecision(18, 2);
        b.Property(p => p.VatRate).HasPrecision(5, 4);
        b.Property(p => p.StockQuantity).HasPrecision(12, 3);
        b.Property(p => p.ReorderLevel).HasPrecision(12, 3);
        b.Ignore(p => p.IsLowStock);
        b.Ignore(p => p.GrossPrice);
        b.HasIndex(p => p.Sku).IsUnique();

        b.HasMany(p => p.Movements).WithOne(m => m.Product).HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.Property(m => m.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(m => m.Quantity).HasPrecision(12, 3);
        b.Property(m => m.Reference).HasMaxLength(150);
        b.Property(m => m.CreatedBy).HasMaxLength(256);
        b.HasIndex(m => new { m.ProductId, m.CreatedAt });
    }
}

internal sealed class VaccinationConfiguration : IEntityTypeConfiguration<Vaccination>
{
    public void Configure(EntityTypeBuilder<Vaccination> b)
    {
        b.Property(v => v.BatchNumber).HasMaxLength(40);
        b.Property(v => v.AdministeredBy).HasMaxLength(256);
        b.HasIndex(v => new { v.PatientId, v.VaccineTypeId, v.AdministeredOn });
        b.HasIndex(v => v.NextDueOn);

        b.HasOne(v => v.Patient).WithMany(p => p.Vaccinations).HasForeignKey(v => v.PatientId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(v => v.VaccineType).WithMany().HasForeignKey(v => v.VaccineTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class VaccineTypeConfiguration : IEntityTypeConfiguration<VaccineType>
{
    public void Configure(EntityTypeBuilder<VaccineType> b)
    {
        b.Property(t => t.Name).HasMaxLength(100).IsRequired();
        b.Property(t => t.Species).HasConversion<string>().HasMaxLength(20);
        b.HasOne(t => t.Product).WithMany().HasForeignKey(t => t.ProductId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.Property(i => i.Number).HasMaxLength(20);
        b.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(i => i.DiscountRate).HasPrecision(5, 4);
        b.Property(i => i.Notes).HasMaxLength(500);
        b.Ignore(i => i.Subtotal);
        b.Ignore(i => i.DiscountAmount);
        b.Ignore(i => i.VatTotal);
        b.Ignore(i => i.Total);
        b.Ignore(i => i.PaidAmount);
        b.Ignore(i => i.Balance);
        b.HasIndex(i => i.Number).IsUnique().HasFilter("[Number] IS NOT NULL");
        b.HasIndex(i => i.Status);

        b.HasOne(i => i.Owner).WithMany(o => o.Invoices).HasForeignKey(i => i.OwnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(i => i.Visit).WithMany().HasForeignKey(i => i.VisitId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(i => i.Payments).WithOne().HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> b)
    {
        b.Property(l => l.Description).HasMaxLength(150).IsRequired();
        b.Property(l => l.Quantity).HasPrecision(10, 3);
        b.Property(l => l.UnitPrice).HasPrecision(18, 2);
        b.Property(l => l.VatRate).HasPrecision(5, 4);
        b.Ignore(l => l.NetAmount);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.Property(p => p.Amount).HasPrecision(18, 2);
        b.Property(p => p.Method).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.Reference).HasMaxLength(100);
        b.Property(p => p.ReceivedBy).HasMaxLength(256);
        b.HasIndex(p => p.PaidAt);
    }
}

internal sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> b)
    {
        b.Property(r => r.Type).HasConversion<string>().HasMaxLength(30);
        b.Property(r => r.Status).HasConversion<string>().HasMaxLength(10);
        b.Property(r => r.Recipient).HasMaxLength(20);
        b.Property(r => r.Message).HasMaxLength(500);
        b.Property(r => r.DedupKey).HasMaxLength(60);
        b.Property(r => r.Error).HasMaxLength(300);
        b.HasIndex(r => r.DedupKey).IsUnique();
        b.HasOne(r => r.Owner).WithMany().HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
