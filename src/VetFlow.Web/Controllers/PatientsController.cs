using Microsoft.AspNetCore.Mvc;
using VetFlow.Application.Interfaces;
using VetFlow.Application.Services;
using VetFlow.Domain.Enums;
using VetFlow.Web.Models;

namespace VetFlow.Web.Controllers;

public class PatientsController(IPatientService patients, IOwnerService owners, ILookupService lookups, IClinicClock clock) : Controller
{
    public async Task<IActionResult> Index(string? q, Species? species, int page = 1, CancellationToken ct = default)
    {
        ViewBag.Query = q;
        ViewBag.Species = species;
        ViewBag.Today = clock.Today;
        return View(await patients.SearchAsync(q, species, page, ct));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        ViewBag.Today = clock.Today;
        ViewBag.Veterinarians = await lookups.GetVeterinariansAsync(true, ct);
        return View(await patients.GetRecordAsync(id, ct));
    }

    public async Task<IActionResult> Create(int ownerId, CancellationToken ct)
    {
        var owner = await owners.GetAsync(ownerId, ct);
        return View("Form", new PatientFormModel { OwnerId = owner.Id, OwnerName = owner.FullName });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PatientFormModel model, CancellationToken ct)
    {
        await ValidateMicrochipAsync(model, null, ct);
        if (!ModelState.IsValid)
        {
            model.OwnerName = (await owners.GetAsync(model.OwnerId, ct)).FullName;
            return View("Form", model);
        }

        var patient = await patients.CreateAsync(model.ToInput(), ct);
        TempData["Success"] = $"{patient.Name} was registered.";
        return RedirectToAction(nameof(Details), new { id = patient.Id });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct) => View("Form", PatientFormModel.From(await patients.GetAsync(id, ct)));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PatientFormModel model, CancellationToken ct)
    {
        model.Id = id;
        await ValidateMicrochipAsync(model, id, ct);
        if (!ModelState.IsValid)
        {
            model.OwnerName = (await owners.GetAsync(model.OwnerId, ct)).FullName;
            return View("Form", model);
        }

        await patients.UpdateAsync(id, model.ToInput(), ct);
        TempData["Success"] = "Patient details saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task ValidateMicrochipAsync(PatientFormModel model, int? excludeId, CancellationToken ct)
    {
        if (model.DateOfBirth > clock.Today)
        {
            ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth cannot be in the future.");
        }

        if (ModelState.IsValid && !string.IsNullOrWhiteSpace(model.MicrochipNumber)
                               && await patients.MicrochipExistsAsync(model.MicrochipNumber, excludeId, ct))
        {
            ModelState.AddModelError(nameof(model.MicrochipNumber), "This microchip is already registered to another patient.");
        }
    }
}
