using ServiceScheduler.Api.Models;

namespace ServiceScheduler.Api.Services.Interface;

public interface ISchedulingService
{
    Task<Appointment?> GetAppointmentByIdAsync(int appointmentId, CancellationToken ct = default);
    Task<(bool Success, string Error, Appointment? Appointment)> BookAppointmentAsync(BookAppointmentRequest request, CancellationToken ct = default);
    Task<(bool Success, string Error)> CancelAppointmentAsync(int appointmentId, string cancelledBy, string reason, CancellationToken ct = default);
}
