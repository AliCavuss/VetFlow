using Microsoft.EntityFrameworkCore;
using VetFlow.Application.Common;
using VetFlow.Application.Interfaces;
using VetFlow.Domain.Common;
using VetFlow.Domain.Entities;
using VetFlow.Domain.Enums;

namespace VetFlow.Application.Services;

public sealed class InvoiceQuery
{
    public InvoiceStatus? Status { get; set; }
    public int? OwnerId { get; set; }
    public string? Search { get; set; }
    public bool UnpaidOnly { get; set; }
    public int Page { get; set; } = 1;
}

public interface IInvoiceService
{
    Task<PagedResult<Invoice>> SearchAsync(InvoiceQuery query, CancellationToken ct = default);
    Task<Invoice> GetAsync(int id, CancellationToken ct = default);
    Task<Invoice> CreateDraftAsync(int ownerId, CancellationToken ct = default);
    Task AddProductLineAsync(int id, int productId, decimal quantity, string user, CancellationToken ct = default);
    Task AddCustomLineAsync(int id, string description, decimal quantity, decimal unitPrice, decimal vatRate, CancellationToken ct = default);
    Task RemoveLineAsync(int id, int lineId, string user, CancellationToken ct = default);
    Task ApplyDiscountAsync(int id, decimal rate, CancellationToken ct = default);
    Task IssueAsync(int id, CancellationToken ct = default);
    Task RegisterPaymentAsync(int id, decimal amount, PaymentMethod method, string? reference, string user, CancellationToken ct = default);
    Task CancelAsync(int id, CancellationToken ct = default);
}

public sealed class InvoiceService(IApplicationDbContext db, IClinicClock clock) : IInvoiceService
{
    public Task<PagedResult<Invoice>> SearchAsync(InvoiceQuery q, CancellationToken ct = default)
    {
        var invoices = db.Invoices.AsNoTracking()
            .Include(i => i.Owner)
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .AsQueryable();

        if (q.Status.HasValue) invoices = invoices.Where(i => i.Status == q.Status.Value);
        if (q.OwnerId.HasValue) invoices = invoices.Where(i => i.OwnerId == q.OwnerId.Value);
        if (q.UnpaidOnly) invoices = invoices.Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            invoices = invoices.Where(i => (i.Number != null && i.Number.Contains(term))
                                           || i.Owner.FirstName.Contains(term) || i.Owner.LastName.Contains(term));
        }

        return PagedResult<Invoice>.CreateAsync(invoices.OrderByDescending(i => i.CreatedAt), q.Page, 20, ct);
    }

    public async Task<Invoice> GetAsync(int id, CancellationToken ct = default) =>
        await db.Invoices.AsNoTracking()
            .Include(i => i.Owner)
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .Include(i => i.Visit).ThenInclude(v => v!.Patient)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
        ?? throw new NotFoundException(nameof(Invoice), id);

    public async Task<Invoice> CreateDraftAsync(int ownerId, CancellationToken ct = default)
    {
        if (!await db.Owners.AnyAsync(o => o.Id == ownerId, ct)) throw new NotFoundException(nameof(Owner), ownerId);
        var invoice = new Invoice { OwnerId = ownerId, CreatedAt = clock.Now };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);
        return invoice;
    }

    /// <summary>Counter sale (e.g. food): the line is priced from the catalogue and stock is taken out immediately.</summary>
    public async Task AddProductLineAsync(int id, int productId, decimal quantity, string user, CancellationToken ct = default)
    {
        var invoice = await LoadAsync(id, ct);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId && p.IsActive, ct)
                      ?? throw new NotFoundException(nameof(Product), productId);

        if (invoice.Status != InvoiceStatus.Draft) throw new DomainException("Only draft invoices can be changed.");
        product.RemoveStock(quantity);
        invoice.AddLine(product.Name, quantity, product.UnitPrice, product.VatRate, product.Id);
        if (product.TracksStock)
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id, Type = StockMovementType.Usage, Quantity = -quantity,
                Reference = $"Invoice draft #{invoice.Id}", CreatedAt = clock.Now, CreatedBy = user
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task AddCustomLineAsync(int id, string description, decimal quantity, decimal unitPrice, decimal vatRate, CancellationToken ct = default)
    {
        var invoice = await LoadAsync(id, ct);
        invoice.AddLine(description, quantity, unitPrice, vatRate);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveLineAsync(int id, int lineId, string user, CancellationToken ct = default)
    {
        var invoice = await LoadAsync(id, ct);
        var line = invoice.Lines.FirstOrDefault(l => l.Id == lineId) ?? throw new NotFoundException("Invoice line", lineId);
        invoice.RemoveLine(line);
        db.InvoiceLines.Remove(line);

        // Stock of counter sales goes back on the shelf; items used in a visit were consumed and stay deducted.
        if (invoice.VisitId is null && line.ProductId.HasValue)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId.Value, ct);
            if (product is { TracksStock: true })
            {
                product.AddStock(line.Quantity);
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = product.Id, Type = StockMovementType.UsageReversed, Quantity = line.Quantity,
                    Reference = $"Invoice draft #{invoice.Id}", CreatedAt = clock.Now, CreatedBy = user
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task ApplyDiscountAsync(int id, decimal rate, CancellationToken ct = default)
    {
        var invoice = await LoadAsync(id, ct);
        invoice.ApplyDiscount(rate);
        await db.SaveChangesAsync(ct);
    }

    public async Task IssueAsync(int id, CancellationToken ct = default)
    {
        var invoice = await LoadAsync(id, ct);
        var now = clock.Now;
        var prefix = $"VF-{now.Year}-";
        var issuedThisYear = await db.Invoices.CountAsync(i => i.Number != null && i.Number.StartsWith(prefix), ct);

        invoice.Issue($"{prefix}{issuedThisYear + 1:D5}", now);
        await db.SaveChangesAsync(ct);
    }

    public async Task RegisterPaymentAsync(int id, decimal amount, PaymentMethod method, string? reference, string user, CancellationToken ct = default)
    {
        var invoice = await LoadAsync(id, ct);
        invoice.RegisterPayment(amount, method, clock.Now, user, reference);
        await db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(int id, CancellationToken ct = default)
    {
        var invoice = await LoadAsync(id, ct);
        invoice.Cancel();
        await db.SaveChangesAsync(ct);
    }

    private async Task<Invoice> LoadAsync(int id, CancellationToken ct) =>
        await db.Invoices.Include(i => i.Lines).Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == id, ct)
        ?? throw new NotFoundException(nameof(Invoice), id);
}
