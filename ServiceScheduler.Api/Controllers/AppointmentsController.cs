using Microsoft.AspNetCore.Authorization;
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
    private readonly ILogger<AppointmentsController> _logger;

    public AppointmentsController(ISchedulingService scheduling, ILogger<AppointmentsController> logger)
    {
        _scheduling = scheduling;
        _logger = logger;
    }

    [Authorize]
    [HttpPost()]
    public async Task<IActionResult> Book([FromBody] BookAppointmentRequest request, CancellationToken ct)
    {
        var (success, error, appointment) = await _scheduling.BookAppointmentAsync(request, ct);
        if (!success)
        {
            _logger.LogWarning("Book conflict: {Error}", error);
            return Conflict(new { error });
        }
        return CreatedAtAction(nameof(GetById), new { id = appointment!.Id }, AppointmentResponse.From(appointment));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById([FromServices] SchedulerDbContext db, int id, CancellationToken ct)
    {
        var appointment = await db.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Vehicle)
            .Include(a => a.ServiceBay)
            .Include(a => a.Technician)
            .Include(a => a.ServiceLines).ThenInclude(sl => sl.ServiceType)
            .Include(a => a.AuditLogs)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        return appointment is null ? NotFound() : Ok(AppointmentResponse.From(appointment));
    }

    [Authorize]
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelAppointmentRequest request, CancellationToken ct)
    {
        var (success, error) = await _scheduling.CancelAppointmentAsync(id, request.CancelledBy, request.Reason, ct);
        if (!success)
        {
            _logger.LogWarning("Cancel conflict: id={AppointmentId} reason={Error}", id, error);
            return Conflict(new { error });
        }
        return NoContent();
    }
}
