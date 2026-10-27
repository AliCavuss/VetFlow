using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetFlow.Application.Services;
using VetFlow.Web.Helpers;

namespace VetFlow.Web.Controllers;

[Authorize(Policy = Policies.ManageInventory)]
public class ReportsController(IReportService reports) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await reports.GetAsync(ct));
}
