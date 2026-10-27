using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetFlow.Application.Interfaces;
using VetFlow.Application.Services;
using VetFlow.Domain.Enums;
using VetFlow.Web.Helpers;
using VetFlow.Web.Models;

namespace VetFlow.Web.Controllers;

public class InvoicesController(IInvoiceService invoices, ILookupService lookups, IConfiguration configuration, IClinicClock clock) : Controller
{
    public async Task<IActionResult> Index([FromQuery] InvoiceQuery query, CancellationToken ct)
    {
        ViewBag.Query = query;
        ViewBag.Today = clock.Today;
        return View(await invoices.SearchAsync(query, ct));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        ViewBag.Today = clock.Today;
        return View(await BuildAsync(id, ct));
    }

    /// <summary>Print-friendly invoice (use the browser's "Print / Save as PDF").</summary>
    public async Task<IActionResult> Print(int id, CancellationToken ct) => View(await BuildAsync(id, ct));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Billing)]
    public async Task<IActionResult> CreateForOwner(int ownerId, CancellationToken ct)
    {
        var invoice = await invoices.CreateDraftAsync(ownerId, ct);
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Billing)]
    public async Task<IActionResult> AddProduct(int id, int productId, decimal quantity, CancellationToken ct)
    {
        await invoices.AddProductLineAsync(id, productId, quantity, User.UserName(), ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Billing)]
    public async Task<IActionResult> AddCustom(int id, string description, decimal quantity, decimal unitPrice, decimal vatPercent, CancellationToken ct)
    {
        await invoices.AddCustomLineAsync(id, description, quantity, unitPrice, vatPercent / 100m, ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Billing)]
    public async Task<IActionResult> RemoveLine(int id, int lineId, CancellationToken ct)
    {
        await invoices.RemoveLineAsync(id, lineId, User.UserName(), ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Billing)]
    public async Task<IActionResult> Discount(int id, decimal percent, CancellationToken ct)
    {
        await invoices.ApplyDiscountAsync(id, percent / 100m, ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Billing)]
    public async Task<IActionResult> Issue(int id, CancellationToken ct)
    {
        await invoices.IssueAsync(id, ct);
        TempData["Success"] = "Invoice issued.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Billing)]
    public async Task<IActionResult> Pay(int id, decimal amount, PaymentMethod method, string? reference, CancellationToken ct)
    {
        await invoices.RegisterPaymentAsync(id, amount, method, reference, User.UserName(), ct);
        TempData["Success"] = $"Payment of {Ui.Money(amount)} registered.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Billing)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        await invoices.CancelAsync(id, ct);
        TempData["Success"] = "Invoice cancelled.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<InvoiceDetailsViewModel> BuildAsync(int id, CancellationToken ct) => new()
    {
        Invoice = await invoices.GetAsync(id, ct),
        Products = await lookups.GetSellableProductsAsync(ct),
        ClinicName = configuration["Clinic:Name"] ?? "VetFlow Veterinary Clinic"
    };
}
