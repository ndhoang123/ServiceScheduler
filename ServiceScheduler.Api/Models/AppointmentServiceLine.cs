namespace ServiceScheduler.Api.Models;

public class AppointmentServiceLine
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;
    public int ServiceTypeId { get; set; }
    public ServiceType ServiceType { get; set; } = null!;
    public int DurationMinutes { get; set; }
    public TechnicianSkill RequiredSkill { get; set; }
    public BayCapabilityTag RequiredBayCapability { get; set; }
}
