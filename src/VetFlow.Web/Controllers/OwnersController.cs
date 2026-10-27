using Microsoft.AspNetCore.Mvc;
using VetFlow.Application.Services;
using VetFlow.Web.Models;

namespace VetFlow.Web.Controllers;

public class OwnersController(IOwnerService owners, IInvoiceService invoices) : Controller
{
    public async Task<IActionResult> Index(string? q, int page = 1, CancellationToken ct = default)
    {
        ViewBag.Query = q;
        return View(await owners.SearchAsync(q, page, ct));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var owner = await owners.GetAsync(id, ct);
        var ownerInvoices = await invoices.SearchAsync(new InvoiceQuery { OwnerId = id }, ct);
        return View(new OwnerDetailsViewModel { Owner = owner, Invoices = ownerInvoices.Items });
    }

    public IActionResult Create() => View("Form", new OwnerFormModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OwnerFormModel model, CancellationToken ct)
    {
        if (ModelState.IsValid && await owners.PhoneExistsAsync(model.Phone, null, ct))
        {
            ModelState.AddModelError(nameof(model.Phone), "An owner with this phone number already exists.");
        }

        if (!ModelState.IsValid) return View("Form", model);

        var owner = await owners.CreateAsync(model.ToInput(), ct);
        TempData["Success"] = $"{owner.FullName} was added. Now register the first patient.";
        return RedirectToAction("Create", "Patients", new { ownerId = owner.Id });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct) => View("Form", OwnerFormModel.From(await owners.GetAsync(id, ct)));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, OwnerFormModel model, CancellationToken ct)
    {
        model.Id = id;
        if (ModelState.IsValid && await owners.PhoneExistsAsync(model.Phone, id, ct))
        {
            ModelState.AddModelError(nameof(model.Phone), "Another owner already uses this phone number.");
        }

        if (!ModelState.IsValid) return View("Form", model);

        await owners.UpdateAsync(id, model.ToInput(), ct);
        TempData["Success"] = "Owner details saved.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
