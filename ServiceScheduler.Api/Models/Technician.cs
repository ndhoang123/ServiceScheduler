namespace ServiceScheduler.Api.Models;

public class Technician
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DealershipLocation { get; set; } = string.Empty;
    public TechnicianSkill Skill { get; set; }
    public bool IsActive { get; set; } = true;
    public TimeOnly ShiftStart { get; set; }
    public TimeOnly ShiftEnd { get; set; }
}
