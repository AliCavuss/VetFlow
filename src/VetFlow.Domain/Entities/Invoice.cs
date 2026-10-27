using VetFlow.Domain.Common;
using VetFlow.Domain.Enums;

namespace VetFlow.Domain.Entities;

/// <summary>
/// Invoice with Turkish VAT (KDV) per line, an optional discount and partial payments.
/// Draft → Issued → Partially paid → Paid (or Cancelled while nothing is paid).
/// </summary>
public class Invoice
{
    public const decimal MaxDiscountRate = 0.50m;

    public int Id { get; set; }

    /// <summary>Assigned when the invoice is issued, e.g. "VF-2026-00042".</summary>
    public string? Number { get; private set; }

    public int OwnerId { get; set; }
    public Owner Owner { get; set; } = null!;

    public int? VisitId { get; set; }
    public Visit? Visit { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? IssuedAt { get; private set; }
    public DateTime? DueDate { get; private set; }
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;

    /// <summary>0.10 = 10 % discount on the net amount.</summary>
    public decimal DiscountRate { get; private set; }

    public string? Notes { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public decimal Subtotal => Lines.Sum(l => l.NetAmount);
    public decimal DiscountAmount => Round(Subtotal * DiscountRate);
    public decimal VatTotal => Lines.Sum(l => Round(l.NetAmount * (1 - DiscountRate) * l.VatRate));
    public decimal Total => Subtotal - DiscountAmount + VatTotal;
    public decimal PaidAmount => Payments.Sum(p => p.Amount);
    public decimal Balance => Total - PaidAmount;

    /// <summary>VAT broken down per rate, as printed on Turkish invoices ("KDV %20").</summary>
    public IReadOnlyList<(decimal Rate, decimal Base, decimal Vat)> VatBreakdown() =>
        Lines.GroupBy(l => l.VatRate)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var net = Round(g.Sum(l => l.NetAmount) * (1 - DiscountRate));
                return (g.Key, net, g.Sum(l => Round(l.NetAmount * (1 - DiscountRate) * l.VatRate)));
            })
            .ToList();

    public bool IsOverdue(DateTime today) =>
        (Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid) && DueDate.HasValue && DueDate.Value.Date < today.Date;

    public static Invoice CreateFromVisit(Visit visit, int ownerId, DateTime now)
    {
        var invoice = new Invoice { OwnerId = ownerId, VisitId = visit.Id, CreatedAt = now };
        foreach (var item in visit.Items)
        {
            invoice.AddLine(item.Description, item.Quantity, item.UnitPrice, item.VatRate, item.ProductId);
        }

        return invoice;
    }

    public InvoiceLine AddLine(string description, decimal quantity, decimal unitPrice, decimal vatRate, int? productId = null)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(description)) throw new DomainException("Line description is required.");
        if (quantity <= 0) throw new DomainException("Quantity must be greater than zero.");
        if (unitPrice < 0) throw new DomainException("Unit price cannot be negative.");
        if (vatRate is < 0 or > 1) throw new DomainException("VAT rate must be between 0 and 1.");

        var line = new InvoiceLine
        {
            Description = description.Trim(),
            Quantity = quantity,
            UnitPrice = unitPrice,
            VatRate = vatRate,
            ProductId = productId
        };
        Lines.Add(line);
        return line;
    }

    public void RemoveLine(InvoiceLine line)
    {
        EnsureDraft();
        if (!Lines.Remove(line)) throw new DomainException("The line does not belong to this invoice.");
    }

    public void ApplyDiscount(decimal rate)
    {
        EnsureDraft();
        if (rate < 0 || rate > MaxDiscountRate)
        {
            throw new DomainException($"Discount must be between 0 % and {MaxDiscountRate:P0}.");
        }

        DiscountRate = rate;
    }

    public void Issue(string number, DateTime now, int paymentTermDays = 14)
    {
        EnsureDraft();
        if (Lines.Count == 0) throw new DomainException("An invoice needs at least one line before it can be issued.");

        Number = number;
        IssuedAt = now;
        DueDate = now.Date.AddDays(paymentTermDays);
        Status = Total == 0 ? InvoiceStatus.Paid : InvoiceStatus.Issued;
    }

    public Payment RegisterPayment(decimal amount, PaymentMethod method, DateTime now, string receivedBy, string? reference = null)
    {
        if (Status is not (InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid))
        {
            throw new DomainException($"Payments can only be registered on issued invoices (this one is '{Status}').");
        }

        if (amount <= 0) throw new DomainException("Payment amount must be greater than zero.");
        if (amount > Balance) throw new DomainException($"Payment exceeds the open balance of {Balance:0.00} TRY.");

        var payment = new Payment
        {
            Amount = amount,
            Method = method,
            PaidAt = now,
            ReceivedBy = receivedBy,
            Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim()
        };
        Payments.Add(payment);
        Status = Balance == 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
        return payment;
    }

    public void Cancel()
    {
        if (Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled || Payments.Count > 0)
        {
            throw new DomainException("Invoices with payments cannot be cancelled.");
        }

        Status = InvoiceStatus.Cancelled;
    }

    private void EnsureDraft()
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new DomainException("Only draft invoices can be changed.");
        }
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public class InvoiceLine
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int? ProductId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }

    public decimal NetAmount => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
}

public class Payment
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public DateTime PaidAt { get; set; }
    public string? Reference { get; set; }
    public string ReceivedBy { get; set; } = string.Empty;
}
