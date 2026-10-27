using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetFlow.Application.Interfaces;
using VetFlow.Application.Services;
using VetFlow.Web.Helpers;

namespace VetFlow.Web.Controllers;

public class VaccinationsController(IVaccinationService vaccinations, IReminderService reminders, IClinicClock clock) : Controller
{
    /// <summary>Boosters that are overdue or due within <paramref name="days"/> days.</summary>
    public async Task<IActionResult> Index(int days = 30, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 0, 365);
        ViewBag.Days = days;
        ViewBag.Today = clock.Today;
        return View(await vaccinations.GetDueAsync(days, ct));
    }

    public async Task<IActionResult> Reminders(CancellationToken ct) => View(await reminders.GetRecentAsync(100, ct));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.ManageInventory)]
    public async Task<IActionResult> RunReminders(CancellationToken ct)
    {
        var result = await reminders.RunAsync(ct);
        TempData["Success"] = $"Reminder job finished: {result.Created} new, {result.Sent} sent, {result.Failed} failed.";
        return RedirectToAction(nameof(Reminders));
    }
}
