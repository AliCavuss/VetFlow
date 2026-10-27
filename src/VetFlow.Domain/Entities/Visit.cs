using VetFlow.Domain.Common;
using VetFlow.Domain.Enums;

namespace VetFlow.Domain.Entities;

/// <summary>
/// A medical record of one examination, written in SOAP format
/// (Subjective, Objective, Assessment, Plan), with the services and products used.
/// </summary>
public class Visit
{
    public int Id { get; set; }

    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public int VeterinarianId { get; set; }
    public Veterinarian Veterinarian { get; set; } = null!;

    public int? AppointmentId { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? ClosedAt { get; private set; }
    public VisitStatus Status { get; private set; } = VisitStatus.Open;

    // Vitals
    public decimal? WeightKg { get; set; }
    public decimal? TemperatureC { get; set; }
    public int? HeartRate { get; set; }

    // SOAP notes
    public string? Subjective { get; set; }
    public string? Objective { get; set; }

    /// <summary>Diagnosis – required to close the visit.</summary>
    public string? Assessment { get; set; }

    public string? Plan { get; set; }

    public ICollection<VisitItem> Items { get; set; } = new List<VisitItem>();

    public bool IsOpen => Status == VisitStatus.Open;
    public decimal NetTotal => Items.Sum(i => i.NetAmount);

    public VisitItem AddItem(Product product, decimal quantity)
    {
        EnsureOpen();
        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than zero.");
        }

        if (!product.IsActive)
        {
            throw new DomainException($"'{product.Name}' is no longer active.");
        }

        var item = new VisitItem
        {
            ProductId = product.Id,
            Description = product.Name,
            Quantity = quantity,
            UnitPrice = product.UnitPrice,
            VatRate = product.VatRate
        };
        Items.Add(item);
        return item;
    }

    public void RemoveItem(VisitItem item)
    {
        EnsureOpen();
        if (!Items.Remove(item))
        {
            throw new DomainException("The item does not belong to this visit.");
        }
    }

    public void Close(DateTime now)
    {
        EnsureOpen();
        if (string.IsNullOrWhiteSpace(Assessment))
        {
            throw new DomainException("Enter an assessment (diagnosis) before closing the visit.");
        }

        Status = VisitStatus.Closed;
        ClosedAt = now;
    }

    public void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new DomainException("This visit is closed and can no longer be changed.");
        }
    }
}

/// <summary>A service, medication or supply used during a visit. Price and VAT are snapshotted.</summary>
public class VisitItem
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }

    public decimal NetAmount => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
}
