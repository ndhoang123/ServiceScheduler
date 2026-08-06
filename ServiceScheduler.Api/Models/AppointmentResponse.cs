namespace ServiceScheduler.Api.Models;

public record AppointmentResponse(
    int Id,
    string DealershipLocation,
    DateTime StartTime,
    DateTime EndTime,
    int TotalDurationMinutes,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    CustomerDto Customer,
    VehicleDto Vehicle,
    ServiceBayDto ServiceBay,
    TechnicianDto Technician,
    List<ServiceLineDto> ServiceLines,
    List<AuditLogDto> AuditLogs)
{
    public static AppointmentResponse From(Appointment a) => new(
        a.Id,
        a.DealershipLocation,
        a.StartTime,
        a.EndTime,
        (int)(a.EndTime - a.StartTime).TotalMinutes,
        a.Status.ToString(),
        a.CreatedAt,
        a.UpdatedAt,
        new CustomerDto(a.Customer.Id, a.Customer.Name, a.Customer.Email, a.Customer.Phone),
        new VehicleDto(a.Vehicle.Id, a.Vehicle.Vin, a.Vehicle.Make, a.Vehicle.Model, a.Vehicle.Year),
        new ServiceBayDto(a.ServiceBay.Id, a.ServiceBay.Name, a.ServiceBay.CapabilityTag.ToString()),
        new TechnicianDto(a.Technician.Id, a.Technician.Name, a.Technician.Skill.ToString()),
        a.ServiceLines.Select(sl => new ServiceLineDto(
            sl.Id,
            sl.DurationMinutes,
            new ServiceTypeDto(sl.ServiceType.Id, sl.ServiceType.Name)
        )).ToList(),
        a.AuditLogs.Select(al => new AuditLogDto(
            al.Id,
            al.FromStatus.ToString(),
            al.ToStatus.ToString(),
            al.ChangedBy,
            al.Reason,
            al.ChangedAt
        )).ToList()
    );
}

public record CustomerDto(int Id, string Name, string Email, string Phone);
public record VehicleDto(int Id, string Vin, string Make, string Model, int Year)
{
    public string DisplayName => $"{Year} {Make} {Model}";
}
public record ServiceBayDto(int Id, string Name, string CapabilityTag);
public record TechnicianDto(int Id, string Name, string Skill);
public record ServiceTypeDto(int Id, string Name);
public record ServiceLineDto(int Id, int DurationMinutes, ServiceTypeDto ServiceType);
public record AuditLogDto(int Id, string FromStatus, string ToStatus, string ChangedBy, string Reason, DateTime ChangedAt);
