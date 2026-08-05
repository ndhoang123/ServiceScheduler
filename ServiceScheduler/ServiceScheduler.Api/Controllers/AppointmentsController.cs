using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Api.Data;
using ServiceScheduler.Api.Models;
using ServiceScheduler.Api.Services;

namespace ServiceScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly ISchedulingService _scheduling;

    public AppointmentsController(ISchedulingService scheduling) => _scheduling = scheduling;

    [HttpPost]
    public async Task<IActionResult> Book([FromBody] BookAppointmentRequest request)
    {
        var (success, error, appointment) = await _scheduling.BookAppointmentAsync(request);
        if (!success)
            return Conflict(new { error });
        return CreatedAtAction(nameof(GetById), new { id = appointment!.Id }, appointment);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById([FromServices] SchedulerDbContext db, int id)
    {
        var appointment = await db.Appointments
            .Include(a => a.ServiceLines)
            .Include(a => a.AuditLogs)
            .FirstOrDefaultAsync(a => a.Id == id);

        return appointment is null ? NotFound() : Ok(appointment);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelAppointmentRequest request)
    {
        var (success, error) = await _scheduling.CancelAppointmentAsync(id, request.CancelledBy, request.Reason);
        if (!success)
            return Conflict(new { error });
        return NoContent();
    }
}
