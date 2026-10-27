using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using VetFlow.Application.Services;

namespace VetFlow.Web.Controllers.Api;

/// <summary>JSON endpoints used by the booking form (free slots, patient autocomplete).</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class SchedulingApiController(IAppointmentService appointments, IPatientService patients) : ControllerBase
{
    /// <summary>Free start times (HH:mm) for a veterinarian on a day.</summary>
    [HttpGet("slots")]
    public async Task<ActionResult<IReadOnlyList<string>>> Slots(
        [FromQuery, Range(1, int.MaxValue)] int vetId,
        [FromQuery] DateTime date,
        [FromQuery, Range(5, 480)] int duration = 30,
        [FromQuery] int? ignoreId = null,
        CancellationToken ct = default)
    {
        var slots = await appointments.GetAvailableSlotsAsync(vetId, date, duration, ignoreId, ct);
        return Ok(slots.Select(s => s.ToString("HH:mm")).ToList());
    }

    [HttpGet("patients/search")]
    public async Task<ActionResult<IReadOnlyList<PatientSearchHit>>> SearchPatients([FromQuery] string term, CancellationToken ct) =>
        Ok(await patients.QuickSearchAsync(term, ct));
}
