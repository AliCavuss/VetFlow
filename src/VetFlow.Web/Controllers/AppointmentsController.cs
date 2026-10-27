using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using VetFlow.Application.Interfaces;
using VetFlow.Application.Services;
using VetFlow.Domain.Enums;
using VetFlow.Infrastructure.Identity;
using VetFlow.Web.Helpers;
using VetFlow.Web.Models;

namespace VetFlow.Web.Controllers;

public class AppointmentsController(
    IAppointmentService appointments,
    IPatientService patients,
    ILookupService lookups,
    IClinicClock clock,
    UserManager<ApplicationUser> users) : Controller
{
    /// <summary>Week calendar (Mon–Sat). <paramref name="date"/> selects the week, <paramref name="vetId"/> filters by veterinarian.</summary>
    public async Task<IActionResult> Index(DateTime? date, int? vetId, CancellationToken ct)
    {
        var day = (date ?? clock.Today).Date;
        var weekStart = day.AddDays(-(((int)day.DayOfWeek + 6) % 7)); // Monday

        if (vetId is null && Request.Query.ContainsKey("vetId") == false)
        {
            vetId = (await users.GetUserAsync(User))?.VeterinarianId;
        }

        var filter = vetId is > 0 ? vetId : null;
        return View(new CalendarViewModel
        {
            WeekStart = weekStart,
            Today = clock.Today,
            VeterinarianId = filter,
            Veterinarians = await lookups.GetVeterinariansAsync(true, ct),
            Appointments = await appointments.GetRangeAsync(weekStart, weekStart.AddDays(7), filter, ct)
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        ViewBag.Now = clock.Now;
        return View(await appointments.GetAsync(id, ct));
    }

    public async Task<IActionResult> Create(int? patientId, DateTime? date, string? time, int? vetId, CancellationToken ct)
    {
        var model = new AppointmentFormModel
        {
            Date = (date ?? clock.Today).Date,
            Time = time ?? string.Empty,
            VeterinarianId = vetId ?? (await users.GetUserAsync(User))?.VeterinarianId ?? 0
        };

        if (patientId.HasValue)
        {
            var patient = await patients.GetAsync(patientId.Value, ct);
            model.PatientId = patient.Id;
            model.PatientLabel = $"{patient.Name} ({patient.Owner.FullName})";
        }

        await PopulateAsync(model, ct);
        return View("Form", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppointmentFormModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            foreach (var error in await appointments.ValidateAsync(model.ToInput(), null, ct))
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(model, ct);
            return View("Form", model);
        }

        var appointment = await appointments.CreateAsync(model.ToInput(), User.UserName(), ct);
        TempData["Success"] = $"Appointment booked for {appointment.StartsAt:dd.MM.yyyy HH:mm}.";
        return RedirectToAction(nameof(Index), new { date = appointment.StartsAt.ToString("yyyy-MM-dd"), vetId = appointment.VeterinarianId });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var appointment = await appointments.GetAsync(id, ct);
        if (appointment.Status != AppointmentStatus.Scheduled)
        {
            TempData["Error"] = "Only scheduled appointments can be changed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = AppointmentFormModel.From(appointment);
        await PopulateAsync(model, ct);
        return View("Form", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AppointmentFormModel model, CancellationToken ct)
    {
        model.Id = id;
        if (ModelState.IsValid)
        {
            foreach (var error in await appointments.ValidateAsync(model.ToInput(), id, ct))
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(model, ct);
            return View("Form", model);
        }

        await appointments.RescheduleAsync(id, model.ToInput(), ct);
        TempData["Success"] = "Appointment updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn(int id, CancellationToken ct)
    {
        await appointments.CheckInAsync(id, ct);
        TempData["Success"] = "Patient checked in – waiting for the veterinarian.";
        return RedirectBack(id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason, CancellationToken ct)
    {
        await appointments.CancelAsync(id, reason, ct);
        TempData["Success"] = "Appointment cancelled – the slot is free again.";
        return RedirectBack(id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NoShow(int id, CancellationToken ct)
    {
        await appointments.MarkNoShowAsync(id, ct);
        TempData["Success"] = "Marked as no-show.";
        return RedirectBack(id);
    }

    [HttpPost, ValidateAntiForgeryToken, Microsoft.AspNetCore.Authorization.Authorize(Policy = Policies.Clinical)]
    public async Task<IActionResult> StartVisit(int id, CancellationToken ct)
    {
        var visitId = await appointments.StartVisitAsync(id, ct);
        return RedirectToAction("Details", "Visits", new { id = visitId });
    }

    private IActionResult RedirectBack(int id)
    {
        var returnUrl = Request.Form["returnUrl"].ToString();
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateAsync(AppointmentFormModel model, CancellationToken ct)
    {
        var vets = await lookups.GetVeterinariansAsync(true, ct);
        model.VeterinarianOptions = vets
            .Select(v => new SelectListItem($"{v.DisplayName} – {v.Specialty}", v.Id.ToString(), v.Id == model.VeterinarianId))
            .ToList();

        if (model.PatientId > 0 && string.IsNullOrEmpty(model.PatientLabel))
        {
            var patient = await patients.GetAsync(model.PatientId, ct);
            model.PatientLabel = $"{patient.Name} ({patient.Owner.FullName})";
        }
    }
}
