using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Api.Data;
using ServiceScheduler.Api.Models;

namespace ServiceScheduler.Api.Services;

public class SchedulingService : ISchedulingService
{
    // mandatory post-appointment recovery buffer (system design §2)
    private const int BufferMinutes = 10;

    private readonly SchedulerDbContext _db;

    public SchedulingService(SchedulerDbContext db) => _db = db;

    public async Task<(bool Success, string Error, Appointment? Appointment)> BookAppointmentAsync(
        BookAppointmentRequest request)
    {
        var serviceTypes = await _db.ServiceTypes
            .Where(st => request.ServiceTypeIds.Contains(st.Id))
            .ToListAsync();

        if (serviceTypes.Count != request.ServiceTypeIds.Count)
            return (false, "One or more service types not found.", null);

        // Step 1: total duration including mandatory buffer
        int totalMinutes = serviceTypes.Sum(st => st.DefaultDurationMinutes) + BufferMinutes;
        var endTime = request.StartTime.AddMinutes(totalMinutes);

        // Steps 2-3: find conflict-free bay and technician
        var bay = await FindAvailableBayAsync(request.DealershipLocation, serviceTypes, request.StartTime, endTime);
        if (bay is null)
            return (false, "No available service bay for the requested time window.", null);

        var technician = await FindAvailableTechnicianAsync(request.DealershipLocation, serviceTypes, request.StartTime, endTime);
        if (technician is null)
            return (false, "No available technician for the requested time window.", null);

        // Step 4: persist inside an ACID transaction
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var appointment = new Appointment
            {
                CustomerId = request.CustomerId,
                VehicleId = request.VehicleId,
                DealershipLocation = request.DealershipLocation,
                ServiceBayId = bay.Id,
                TechnicianId = technician.Id,
                StartTime = request.StartTime,
                EndTime = endTime,
                Status = AppointmentStatus.Confirmed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _db.Appointments.Add(appointment);
            await _db.SaveChangesAsync();

            foreach (var st in serviceTypes)
            {
                _db.AppointmentServiceLines.Add(new AppointmentServiceLine
                {
                    AppointmentId = appointment.Id,
                    ServiceTypeId = st.Id,
                    DurationMinutes = st.DefaultDurationMinutes,
                    RequiredSkill = st.RequiredSkill,
                    RequiredBayCapability = st.RequiredBayCapability,
                });
            }

            _db.AppointmentAuditLogs.Add(new AppointmentAuditLog
            {
                AppointmentId = appointment.Id,
                FromStatus = AppointmentStatus.Pending,
                ToStatus = AppointmentStatus.Confirmed,
                ChangedBy = request.AdvisorId,
                Reason = "Appointment booked.",
                ChangedAt = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, string.Empty, appointment);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<(bool Success, string Error)> CancelAppointmentAsync(
        int appointmentId, string cancelledBy, string reason)
    {
        var appointment = await _db.Appointments.FindAsync(appointmentId);
        if (appointment is null)
            return (false, "Appointment not found.");
        if (appointment.Status == AppointmentStatus.Cancelled)
            return (false, "Appointment is already cancelled.");
        if (appointment.Status == AppointmentStatus.Completed)
            return (false, "Completed appointments cannot be cancelled.");

        var fromStatus = appointment.Status;
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.UpdatedAt = DateTime.UtcNow;

        _db.AppointmentAuditLogs.Add(new AppointmentAuditLog
        {
            AppointmentId = appointmentId,
            FromStatus = fromStatus,
            ToStatus = AppointmentStatus.Cancelled,
            ChangedBy = cancelledBy,
            Reason = reason,
            ChangedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
        // Cancelled appointments are excluded from collision checks — resources are immediately freed
        return (true, string.Empty);
    }

    // Finds the first bay at the location whose capability satisfies all service lines
    // and that has no active appointment overlapping [start, end).
    // Overlap condition: existingStart < requestedEnd && existingEnd > requestedStart
    private async Task<ServiceBay?> FindAvailableBayAsync(
        string location, List<ServiceType> serviceTypes, DateTime start, DateTime end)
    {
        var minCapability = serviceTypes.Max(st => st.RequiredBayCapability);

        var busyBayIds = await _db.Appointments
            .Where(a => a.DealershipLocation == location
                     && a.Status != AppointmentStatus.Cancelled
                     && a.Status != AppointmentStatus.Completed
                     && a.StartTime < end
                     && a.EndTime > start)
            .Select(a => a.ServiceBayId)
            .Distinct()
            .ToListAsync();

        return await _db.ServiceBays
            .Where(b => b.DealershipLocation == location
                     && b.IsActive
                     && b.CapabilityTag >= minCapability
                     && !busyBayIds.Contains(b.Id))
            .FirstOrDefaultAsync();
    }

    // Finds the first technician at the location whose skill satisfies all service lines
    // and that has no active appointment overlapping [start, end).
    private async Task<Technician?> FindAvailableTechnicianAsync(
        string location, List<ServiceType> serviceTypes, DateTime start, DateTime end)
    {
        var minSkill = serviceTypes.Max(st => st.RequiredSkill);

        var busyTechIds = await _db.Appointments
            .Where(a => a.DealershipLocation == location
                     && a.Status != AppointmentStatus.Cancelled
                     && a.Status != AppointmentStatus.Completed
                     && a.StartTime < end
                     && a.EndTime > start)
            .Select(a => a.TechnicianId)
            .Distinct()
            .ToListAsync();

        return await _db.Technicians
            .Where(t => t.DealershipLocation == location
                     && t.IsActive
                     && t.Skill >= minSkill
                     && !busyTechIds.Contains(t.Id))
            .FirstOrDefaultAsync();
    }
}
