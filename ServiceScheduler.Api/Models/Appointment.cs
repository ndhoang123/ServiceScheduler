namespace ServiceScheduler.Api.Models;

public class Appointment
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public string DealershipLocation { get; set; } = string.Empty;
    public int ServiceBayId { get; set; }
    public ServiceBay ServiceBay { get; set; } = null!;
    public int TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AppointmentServiceLine> ServiceLines { get; set; } = new List<AppointmentServiceLine>();
    public ICollection<AppointmentAuditLog> AuditLogs { get; set; } = new List<AppointmentAuditLog>();
}
