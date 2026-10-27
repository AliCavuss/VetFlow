using VetFlow.Domain.Common;
using VetFlow.Domain.Entities;
using VetFlow.Domain.Enums;

namespace VetFlow.Tests;

public class InvoiceTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 14, 0, 0);

    private static Invoice Sample()
    {
        var invoice = new Invoice { OwnerId = 1, CreatedAt = Now };
        invoice.AddLine("General examination", 1, 850m, 0.20m);
        invoice.AddLine("Amoxicillin 250 mg", 10, 18m, 0.10m);
        return invoice;
    }

    [Fact]
    public void Totals_include_vat_per_rate()
    {
        var invoice = Sample();

        Assert.Equal(1030m, invoice.Subtotal);  // 850 + 180
        Assert.Equal(188m, invoice.VatTotal);   // 170 + 18
        Assert.Equal(1218m, invoice.Total);

        var breakdown = invoice.VatBreakdown();
        Assert.Equal(2, breakdown.Count);
        Assert.Equal((0.20m, 850m, 170m), breakdown[0]);
    }

    [Fact]
    public void Discount_reduces_net_and_vat()
    {
        var invoice = Sample();
        invoice.ApplyDiscount(0.10m);

        Assert.Equal(103m, invoice.DiscountAmount);
        Assert.Equal(169.2m, invoice.VatTotal);  // 153 + 16.2
        Assert.Equal(1096.2m, invoice.Total);
    }

    [Fact]
    public void Discount_is_capped() => Assert.Throws<DomainException>(() => Sample().ApplyDiscount(0.6m));

    [Fact]
    public void Partial_then_full_payment()
    {
        var invoice = Sample();
        invoice.Issue("VF-2026-00001", Now);
        Assert.Equal(Now.Date.AddDays(14), invoice.DueDate);

        invoice.RegisterPayment(500m, PaymentMethod.Cash, Now, "desk");
        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Equal(718m, invoice.Balance);

        invoice.RegisterPayment(718m, PaymentMethod.CreditCard, Now, "desk");
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(0m, invoice.Balance);
    }

    [Fact]
    public void Overpayment_and_payment_on_draft_are_rejected()
    {
        var invoice = Sample();
        Assert.Throws<DomainException>(() => invoice.RegisterPayment(10m, PaymentMethod.Cash, Now, "desk"));

        invoice.Issue("VF-2026-00002", Now);
        Assert.Throws<DomainException>(() => invoice.RegisterPayment(5000m, PaymentMethod.Cash, Now, "desk"));
    }

    [Fact]
    public void Issued_invoice_is_locked_and_overdue_after_term()
    {
        var invoice = Sample();
        invoice.Issue("VF-2026-00003", Now);

        Assert.Throws<DomainException>(() => invoice.AddLine("Extra", 1, 10m, 0.2m));
        Assert.False(invoice.IsOverdue(Now.AddDays(14)));
        Assert.True(invoice.IsOverdue(Now.AddDays(15)));
    }

    [Fact]
    public void Empty_invoice_cannot_be_issued() =>
        Assert.Throws<DomainException>(() => new Invoice { CreatedAt = Now }.Issue("VF-2026-00004", Now));
}

public class ProductStockTests
{
    private static Product Amoxicillin()
    {
        var p = new Product { Name = "Amoxicillin", Unit = "tablet", TracksStock = true, ReorderLevel = 20, IsActive = true, UnitPrice = 18m };
        p.AddStock(30);
        return p;
    }

    [Fact]
    public void Removing_stock_below_zero_is_rejected()
    {
        var p = Amoxicillin();
        p.RemoveStock(25);
        Assert.True(p.IsLowStock);
        Assert.Throws<DomainException>(() => p.RemoveStock(6));
        Assert.Equal(5m, p.StockQuantity);
    }

    [Fact]
    public void Stock_take_returns_difference()
    {
        var p = Amoxicillin();
        Assert.Equal(-2m, p.AdjustTo(28));
        Assert.Equal(28m, p.StockQuantity);
    }

    [Fact]
    public void Services_have_no_stock()
    {
        var exam = new Product { Name = "Exam", TracksStock = false, IsActive = true };
        exam.RemoveStock(1); // no-op
        Assert.False(exam.IsLowStock);
        Assert.Throws<DomainException>(() => exam.AddStock(1));
    }
}

public class VisitAndVaccinationTests
{
    private static readonly DateTime Today = new(2026, 10, 22);

    [Fact]
    public void Visit_cannot_close_without_assessment()
    {
        var visit = new Visit { StartedAt = Today };
        Assert.Throws<DomainException>(() => visit.Close(Today));

        visit.Assessment = "Otitis externa";
        visit.Close(Today);
        Assert.False(visit.IsOpen);
        Assert.Throws<DomainException>(() => visit.AddItem(new Product { Name = "x", IsActive = true }, 1));
    }

    [Fact]
    public void Visit_items_snapshot_price_and_vat()
    {
        var visit = new Visit { StartedAt = Today };
        var product = new Product { Id = 3, Name = "Exam", UnitPrice = 850m, VatRate = 0.20m, IsActive = true, TracksStock = false };

        var item = visit.AddItem(product, 1);
        product.UnitPrice = 999m;

        Assert.Equal(850m, item.UnitPrice);
        Assert.Equal(850m, visit.NetTotal);
    }

    [Fact]
    public void Booster_date_and_due_status()
    {
        var dog = new Patient { Id = 1, Name = "Karabaş", Species = Species.Dog };
        var rabies = new VaccineType { Id = 1, Name = "Rabies", IntervalDays = 365 };

        var v = Vaccination.Record(dog, rabies, Today.AddDays(-350), "vet");

        Assert.Equal(Today.AddDays(15), v.NextDueOn);
        Assert.Equal(VaccinationDueStatus.DueSoon, v.DueStatus(Today));
        Assert.Equal(VaccinationDueStatus.Overdue, v.DueStatus(Today.AddDays(16)));
        Assert.Equal(VaccinationDueStatus.UpToDate, v.DueStatus(Today.AddDays(-30)));
    }

    [Fact]
    public void Species_specific_vaccine_is_enforced()
    {
        var cat = new Patient { Id = 2, Name = "Mia", Species = Species.Cat };
        var dhppi = new VaccineType { Id = 2, Name = "DHPPi", Species = Species.Dog, IntervalDays = 365 };

        Assert.Throws<DomainException>(() => Vaccination.Record(cat, dhppi, Today, "vet"));
    }
}
