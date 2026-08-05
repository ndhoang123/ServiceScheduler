using ServiceScheduler.Api.Models;

namespace ServiceScheduler.Api.Services;

public interface ISchedulingService
{
    Task<(bool Success, string Error, Appointment? Appointment)> BookAppointmentAsync(BookAppointmentRequest request);
    Task<(bool Success, string Error)> CancelAppointmentAsync(int appointmentId, string cancelledBy, string reason);
}
