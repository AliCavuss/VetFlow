using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VetFlow.Application.Services;
using VetFlow.Infrastructure.Identity;

namespace VetFlow.Web.Controllers;

public class DashboardController(IDashboardService dashboard, UserManager<ApplicationUser> users, ILookupService lookups) : Controller
{
    /// <summary>Front-desk overview. Veterinarians see their own schedule by default (<c>?vetId=0</c> shows everyone).</summary>
    public async Task<IActionResult> Index(int? vetId, CancellationToken ct)
    {
        if (vetId is null)
        {
            var user = await users.GetUserAsync(User);
            vetId = user?.VeterinarianId;
        }

        var filter = vetId is > 0 ? vetId : null;
        ViewBag.VetId = filter;
        ViewBag.Veterinarians = await lookups.GetVeterinariansAsync(true, ct);
        return View(await dashboard.GetAsync(filter, ct));
    }
}
