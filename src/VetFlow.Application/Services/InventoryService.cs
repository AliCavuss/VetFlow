using Microsoft.EntityFrameworkCore;
using VetFlow.Application.Interfaces;
using VetFlow.Domain.Common;
using VetFlow.Domain.Entities;
using VetFlow.Domain.Enums;

namespace VetFlow.Application.Services;

public sealed record ProductInput(
    string Name, string Sku, ProductCategory Category, string Unit, decimal UnitPrice, decimal VatRate,
    bool TracksStock, decimal ReorderLevel, bool IsActive, decimal? InitialStock);

public sealed class ProductDetails
{
    public required Product Product { get; init; }
    public IReadOnlyList<StockMovement> Movements { get; init; } = [];
}

public interface IInventoryService
{
    Task<IReadOnlyList<Product>> SearchAsync(string? query, ProductCategory? category, bool lowStockOnly, bool includeInactive, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetLowStockAsync(CancellationToken ct = default);
    Task<ProductDetails> GetAsync(int id, CancellationToken ct = default);
    Task<bool> SkuExistsAsync(string sku, int? excludeId, CancellationToken ct = default);
    Task<Product> CreateAsync(ProductInput input, string user, CancellationToken ct = default);
    Task UpdateAsync(int id, ProductInput input, CancellationToken ct = default);
    Task RestockAsync(int id, decimal quantity, string? reference, string user, CancellationToken ct = default);
    Task AdjustAsync(int id, decimal countedQuantity, string? reason, string user, CancellationToken ct = default);
}

public sealed class InventoryService(IApplicationDbContext db, IClinicClock clock) : IInventoryService
{
    public async Task<IReadOnlyList<Product>> SearchAsync(
        string? query, ProductCategory? category, bool lowStockOnly, bool includeInactive, CancellationToken ct = default)
    {
        var products = db.Products.AsNoTracking();
        if (!includeInactive) products = products.Where(p => p.IsActive);
        if (category.HasValue) products = products.Where(p => p.Category == category.Value);
        if (lowStockOnly) products = products.Where(p => p.TracksStock && p.StockQuantity <= p.ReorderLevel);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            products = products.Where(p => p.Name.Contains(term) || p.Sku.Contains(term));
        }

        return await products.OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Product>> GetLowStockAsync(CancellationToken ct = default) =>
        await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.TracksStock && p.StockQuantity <= p.ReorderLevel)
            .OrderBy(p => p.StockQuantity)
            .ToListAsync(ct);

    public async Task<ProductDetails> GetAsync(int id, CancellationToken ct = default)
    {
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException(nameof(Product), id);
        var movements = await db.StockMovements.AsNoTracking()
            .Where(m => m.ProductId == id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(ct);
        return new ProductDetails { Product = product, Movements = movements };
    }

    public Task<bool> SkuExistsAsync(string sku, int? excludeId, CancellationToken ct = default)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        return db.Products.AnyAsync(p => p.Sku == normalized && (excludeId == null || p.Id != excludeId), ct);
    }

    public async Task<Product> CreateAsync(ProductInput input, string user, CancellationToken ct = default)
    {
        var product = new Product();
        Apply(product, input);
        db.Products.Add(product);

        if (product.TracksStock && input.InitialStock is > 0)
        {
            product.AddStock(input.InitialStock.Value);
            product.Movements.Add(new StockMovement
            {
                Type = StockMovementType.Purchase, Quantity = input.InitialStock.Value,
                Reference = "Opening stock", CreatedAt = clock.Now, CreatedBy = user
            });
        }

        await db.SaveChangesAsync(ct);
        return product;
    }

    public async Task UpdateAsync(int id, ProductInput input, CancellationToken ct = default)
    {
        var product = await LoadAsync(id, ct);
        if (product.TracksStock && !input.TracksStock && product.StockQuantity > 0)
        {
            throw new DomainException("Set the stock to zero before turning off stock tracking.");
        }

        Apply(product, input);
        await db.SaveChangesAsync(ct);
    }

    public async Task RestockAsync(int id, decimal quantity, string? reference, string user, CancellationToken ct = default)
    {
        var product = await LoadAsync(id, ct);
        product.AddStock(quantity);
        db.StockMovements.Add(new StockMovement
        {
            ProductId = id, Type = StockMovementType.Purchase, Quantity = quantity,
            Reference = string.IsNullOrWhiteSpace(reference) ? "Goods received" : reference.Trim(),
            CreatedAt = clock.Now, CreatedBy = user
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task AdjustAsync(int id, decimal countedQuantity, string? reason, string user, CancellationToken ct = default)
    {
        var product = await LoadAsync(id, ct);
        var delta = product.AdjustTo(countedQuantity);
        if (delta != 0)
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = id, Type = StockMovementType.Adjustment, Quantity = delta,
                Reference = string.IsNullOrWhiteSpace(reason) ? "Stock-take" : reason.Trim(),
                CreatedAt = clock.Now, CreatedBy = user
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<Product> LoadAsync(int id, CancellationToken ct) =>
        await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException(nameof(Product), id);

    private static void Apply(Product product, ProductInput input)
    {
        if (input.VatRate is < 0 or > 1) throw new DomainException("VAT rate must be between 0 % and 100 %.");
        if (input.UnitPrice < 0) throw new DomainException("Price cannot be negative.");

        product.Name = input.Name.Trim();
        product.Sku = input.Sku.Trim().ToUpperInvariant();
        product.Category = input.Category;
        product.Unit = input.Unit.Trim();
        product.UnitPrice = input.UnitPrice;
        product.VatRate = input.VatRate;
        product.TracksStock = input.Category != ProductCategory.Service && input.TracksStock;
        product.ReorderLevel = product.TracksStock ? Math.Max(0, input.ReorderLevel) : 0;
        product.IsActive = input.IsActive;
    }
}
