using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VetFlow.Application.Interfaces;
using VetFlow.Application.Services;
using VetFlow.Domain.Common;
using VetFlow.Domain.Entities;
using VetFlow.Domain.Enums;
using VetFlow.Infrastructure.Data;

namespace VetFlow.Tests;

internal sealed class FixedClock(DateTime now) : IClinicClock
{
    public DateTime Now { get; set; } = now;
    public DateTime Today => Now.Date;
}

internal sealed class RecordingSms : ISmsSender
{
    public List<(string Phone, string Message)> Sent { get; } = [];
    public Task SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        Sent.Add((phoneNumber, message));
        return Task.CompletedTask;
    }
}

/// <summary>Application services against the EF Core in-memory provider.</summary>
public class ServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 8, 0, 0); // Wednesday morning

    private static async Task<(ApplicationDbContext Db, Patient Dog, Veterinarian Vet, Product Exam, Product Tablets)> SeedAsync()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var owner = new Owner { FirstName = "Ahmet", LastName = "Yılmaz", Phone = "+905324112790", ReminderConsent = true, CreatedAt = Now };
        var dog = new Patient { Owner = owner, Name = "Karabaş", Species = Species.Dog, CreatedAt = Now };
        var vet = new Veterinarian { FullName = "Elif Kaya" };
        var exam = new Product { Name = "General examination", Sku = "SRV-EXAM", Category = ProductCategory.Service, UnitPrice = 850m, VatRate = 0.2m, TracksStock = false, IsActive = true };
        var tablets = new Product { Name = "Amoxicillin", Sku = "MED-AMOX", Category = ProductCategory.Medication, Unit = "tablet", UnitPrice = 18m, VatRate = 0.1m, TracksStock = true, IsActive = true, ReorderLevel = 5 };
        tablets.AddStock(20);

        db.AddRange(owner, dog, vet, exam, tablets);
        await db.SaveChangesAsync();
        return (db, dog, vet, exam, tablets);
    }

    [Fact]
    public async Task Booking_rejects_double_booking_of_the_same_vet()
    {
        var (db, dog, vet, _, _) = await SeedAsync();
        var service = new AppointmentService(db, new FixedClock(Now));
        var start = new DateTime(2026, 10, 22, 10, 0, 0);

        await service.CreateAsync(new AppointmentInput(dog.Id, vet.Id, AppointmentType.Checkup, start, 30, null), "desk");

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(new AppointmentInput(dog.Id, vet.Id, AppointmentType.Checkup, start.AddMinutes(15), 30, null), "desk"));
        Assert.Contains("already has an appointment", ex.Message);

        var slots = await service.GetAvailableSlotsAsync(vet.Id, start, 30, null, CancellationToken.None);
        Assert.DoesNotContain(start, slots);
        Assert.Contains(start.AddMinutes(30), slots);
    }

    [Fact]
    public async Task Full_visit_flow_deducts_stock_and_creates_invoice()
    {
        var (db, dog, vet, exam, tablets) = await SeedAsync();
        var clock = new FixedClock(new DateTime(2026, 10, 22, 10, 0, 0));
        var appointments = new AppointmentService(db, new FixedClock(Now));
        var visits = new VisitService(db, clock);

        var appointment = await appointments.CreateAsync(
            new AppointmentInput(dog.Id, vet.Id, AppointmentType.Checkup, new DateTime(2026, 10, 22, 10, 0, 0), 30, "Ear scratching"), "desk");
        var visitId = await appointments.StartVisitAsync(appointment.Id);

        await visits.SaveNotesAsync(visitId, new VisitNotesInput(31.5m, 38.6m, 96, "Ear scratching", "Redness both ears", "Otitis externa", "Ear drops 10 days"));
        await visits.AddItemAsync(visitId, exam.Id, 1, "vet");
        await visits.AddItemAsync(visitId, tablets.Id, 10, "vet");

        Assert.Equal(10m, (await db.Products.SingleAsync(p => p.Id == tablets.Id)).StockQuantity);
        await Assert.ThrowsAsync<DomainException>(() => visits.AddItemAsync(visitId, tablets.Id, 11, "vet"));

        var invoiceId = await visits.CloseAsync(visitId);

        var invoice = await db.Invoices.Include(i => i.Lines).SingleAsync(i => i.Id == invoiceId);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(1030m, invoice.Subtotal);
        Assert.Equal(AppointmentStatus.Completed, (await db.Appointments.SingleAsync()).Status);
        Assert.Equal(31.5m, (await db.Patients.SingleAsync()).LastWeightKg);
        Assert.Equal(1, await db.StockMovements.CountAsync(m => m.Type == StockMovementType.Usage));
    }

    [Fact]
    public async Task Invoice_numbers_are_sequential_per_year()
    {
        var (db, dog, _, exam, _) = await SeedAsync();
        var service = new InvoiceService(db, new FixedClock(new DateTime(2026, 3, 1, 12, 0, 0)));

        var first = await service.CreateDraftAsync(dog.OwnerId);
        await service.AddProductLineAsync(first.Id, exam.Id, 1, "desk");
        await service.IssueAsync(first.Id);

        var second = await service.CreateDraftAsync(dog.OwnerId);
        await service.AddCustomLineAsync(second.Id, "Nail trim", 1, 150m, 0.2m);
        await service.IssueAsync(second.Id);

        var numbers = await db.Invoices.OrderBy(i => i.Id).Select(i => i.Number).ToListAsync();
        Assert.Equal(new[] { "VF-2026-00001", "VF-2026-00002" }, numbers.ToArray());
    }

    [Fact]
    public async Task Reminder_job_sends_once_per_due_vaccination()
    {
        var (db, dog, _, _, _) = await SeedAsync();
        var clock = new FixedClock(Now);
        var rabies = new VaccineType { Name = "Rabies", IntervalDays = 365 };
        db.VaccineTypes.Add(rabies);
        await db.SaveChangesAsync();
        db.Vaccinations.Add(Vaccination.Record(dog, rabies, Now.Date.AddDays(-360), "vet")); // due in 5 days
        await db.SaveChangesAsync();

        var sms = new RecordingSms();
        var service = new ReminderService(db, new VaccinationService(db, clock), sms, clock, NullLogger<ReminderService>.Instance);

        var first = await service.RunAsync();
        var second = await service.RunAsync();

        Assert.Equal(1, first.Created);
        Assert.Equal(0, second.Created);
        Assert.Single(sms.Sent);
        Assert.Contains("Karabaş", sms.Sent[0].Message);
    }
}
