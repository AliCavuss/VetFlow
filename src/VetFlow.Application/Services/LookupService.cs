using Microsoft.EntityFrameworkCore;
using VetFlow.Application.Interfaces;
using VetFlow.Domain.Entities;
using VetFlow.Domain.Enums;

namespace VetFlow.Application.Services;

/// <summary>Small reference lists used by drop-downs.</summary>
public interface ILookupService
{
    Task<IReadOnlyList<Veterinarian>> GetVeterinariansAsync(bool activeOnly = true, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetSellableProductsAsync(CancellationToken ct = default);
}

public sealed class LookupService(IApplicationDbContext db) : ILookupService
{
    public async Task<IReadOnlyList<Veterinarian>> GetVeterinariansAsync(bool activeOnly = true, CancellationToken ct = default) =>
        await db.Veterinarians.AsNoTracking()
            .Where(v => !activeOnly || v.IsActive)
            .OrderBy(v => v.FullName)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Product>> GetSellableProductsAsync(CancellationToken ct = default) =>
        await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Category != ProductCategory.Vaccine)
            .OrderBy(p => p.Category).ThenBy(p => p.Name)
            .ToListAsync(ct);
}
