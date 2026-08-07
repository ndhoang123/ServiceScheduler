using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceScheduler.Api.Models;
using ServiceScheduler.Api.Services.Interface;

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

    [Authorize(Roles = "ServiceAdvisor")]
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

    [Authorize(Roles = "ServiceAdvisor,Admin")]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var appointment = await _scheduling.GetAppointmentByIdAsync(id, ct);
        return appointment is null ? NotFound() : Ok(AppointmentResponse.From(appointment));
    }

    [Authorize(Roles = "ServiceAdvisor")]
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
