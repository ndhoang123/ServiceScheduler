namespace ServiceScheduler.Api.Models;

public class AppointmentAuditLog
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;
    public AppointmentStatus FromStatus { get; set; }
    public AppointmentStatus ToStatus { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
