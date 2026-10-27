using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetFlow.Application.Services;
using VetFlow.Web.Helpers;
using VetFlow.Web.Models;

namespace VetFlow.Web.Controllers;

/// <summary>Medical records. Everyone can read them; only veterinarians (and admins) can write.</summary>
public class VisitsController(IVisitService visits, IInventoryService inventory, IVaccinationService vaccinations) : Controller
{
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var details = await visits.GetAsync(id, ct);
        return View(new VisitViewModel
        {
            Details = details,
            Notes = VisitNotesFormModel.From(details.Visit),
            Products = (await inventory.SearchAsync(null, null, false, false, ct))
                .Where(p => p.Category != Domain.Enums.ProductCategory.Vaccine).ToList(),
            VaccineTypes = await vaccinations.GetVaccineTypesAsync(details.Visit.Patient.Species, ct)
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Clinical)]
    public async Task<IActionResult> StartWalkIn(int patientId, int veterinarianId, CancellationToken ct)
    {
        var id = await visits.StartWalkInAsync(patientId, veterinarianId, ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Clinical)]
    public async Task<IActionResult> SaveNotes(int id, [Bind(Prefix = "Notes")] VisitNotesFormModel notes, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Details), new { id });
        }

        await visits.SaveNotesAsync(id, notes.ToInput(), ct);
        TempData["Success"] = "Medical notes saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Clinical)]
    public async Task<IActionResult> AddItem(int id, int productId, decimal quantity, CancellationToken ct)
    {
        await visits.AddItemAsync(id, productId, quantity, User.UserName(), ct);
        return RedirectToAction(nameof(Details), null, new { id }, "items");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Clinical)]
    public async Task<IActionResult> RemoveItem(int id, int itemId, CancellationToken ct)
    {
        await visits.RemoveItemAsync(id, itemId, User.UserName(), ct);
        return RedirectToAction(nameof(Details), null, new { id }, "items");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Clinical)]
    public async Task<IActionResult> AddVaccination(int id, int vaccineTypeId, string? batchNumber, CancellationToken ct)
    {
        await visits.RecordVaccinationAsync(id, vaccineTypeId, batchNumber, User.UserName(), ct);
        TempData["Success"] = "Vaccination recorded – the booster date was calculated automatically.";
        return RedirectToAction(nameof(Details), null, new { id }, "vaccinations");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Clinical)]
    public async Task<IActionResult> Close(int id, CancellationToken ct)
    {
        var invoiceId = await visits.CloseAsync(id, ct);
        if (invoiceId.HasValue)
        {
            TempData["Success"] = "Visit closed. A draft invoice was created from the services and products used.";
            return RedirectToAction("Details", "Invoices", new { id = invoiceId });
        }

        TempData["Success"] = "Visit closed.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
