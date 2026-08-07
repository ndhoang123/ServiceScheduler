namespace ServiceScheduler.Api.Models;

public record BookAppointmentRequest
{
    public int CustomerId { get; set; }
    public int VehicleId { get; set; }
    public string DealershipLocation { get; set; } = string.Empty;
    public List<int> ServiceTypeIds { get; set; } = new();
    public DateTime StartTime { get; set; }
    public string AdvisorId { get; set; } = string.Empty;
}

public class CancelAppointmentRequest
{
    public string CancelledBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class AppointmentTransitionRequest
{
    public string ChangedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
