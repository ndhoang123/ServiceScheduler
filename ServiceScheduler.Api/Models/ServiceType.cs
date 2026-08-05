namespace ServiceScheduler.Api.Models;

public class ServiceType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DefaultDurationMinutes { get; set; }
    public TechnicianSkill RequiredSkill { get; set; }
    public BayCapabilityTag RequiredBayCapability { get; set; }
}
