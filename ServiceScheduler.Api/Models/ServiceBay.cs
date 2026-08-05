namespace ServiceScheduler.Api.Models;

public class ServiceBay
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DealershipLocation { get; set; } = string.Empty;
    public BayCapabilityTag CapabilityTag { get; set; }
    public bool IsActive { get; set; } = true;
}
