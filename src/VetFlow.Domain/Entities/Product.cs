using VetFlow.Domain.Common;
using VetFlow.Domain.Enums;

namespace VetFlow.Domain.Entities;

/// <summary>Anything the clinic sells or uses: services (not stocked), medications, vaccines, supplies, food.</summary>
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public ProductCategory Category { get; set; }

    /// <summary>Unit of measure, e.g. "dose", "tablet", "ml", "pcs", "session".</summary>
    public string Unit { get; set; } = "pcs";

    /// <summary>Net unit price in TRY (VAT excluded).</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>VAT (KDV) rate, e.g. 0.20 for 20 %.</summary>
    public decimal VatRate { get; set; } = 0.20m;

    /// <summary>Services are not stocked.</summary>
    public bool TracksStock { get; set; } = true;

    public decimal StockQuantity { get; private set; }
    public decimal ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();

    public bool IsLowStock => TracksStock && StockQuantity <= ReorderLevel;
    public decimal GrossPrice => Math.Round(UnitPrice * (1 + VatRate), 2, MidpointRounding.AwayFromZero);

    public void AddStock(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than zero.");
        }

        EnsureTracksStock();
        StockQuantity += quantity;
    }

    /// <summary>Takes stock out (e.g. used in a visit). Services ignore stock.</summary>
    public void RemoveStock(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than zero.");
        }

        if (!TracksStock)
        {
            return;
        }

        if (StockQuantity < quantity)
        {
            throw new DomainException(
                $"Not enough '{Name}' in stock: {StockQuantity:0.##} {Unit} available, {quantity:0.##} requested.");
        }

        StockQuantity -= quantity;
    }

    /// <summary>Sets the stock to a counted value (stock-take) and returns the difference.</summary>
    public decimal AdjustTo(decimal countedQuantity)
    {
        if (countedQuantity < 0)
        {
            throw new DomainException("Counted quantity cannot be negative.");
        }

        EnsureTracksStock();
        var delta = countedQuantity - StockQuantity;
        StockQuantity = countedQuantity;
        return delta;
    }

    private void EnsureTracksStock()
    {
        if (!TracksStock)
        {
            throw new DomainException($"'{Name}' is a service and has no stock.");
        }
    }
}

public class StockMovement
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public StockMovementType Type { get; set; }

    /// <summary>Signed quantity: positive = in, negative = out.</summary>
    public decimal Quantity { get; set; }

    public string? Reference { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
