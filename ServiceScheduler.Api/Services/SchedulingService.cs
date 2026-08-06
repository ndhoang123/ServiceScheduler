using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Api.Data;
using ServiceScheduler.Api.Models;

namespace ServiceScheduler.Api.Services;

public class SchedulingService : ISchedulingService
{
    // mandatory post-appointment recovery buffer (system design §2)
    private const int BufferMinutes = 10;

    private readonly SchedulerDbContext _db;
    private readonly ILogger<SchedulingService> _logger;

    public SchedulingService(SchedulerDbContext db, ILogger<SchedulingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(bool Success, string Error, Appointment? Appointment)> BookAppointmentAsync(
        BookAppointmentRequest request, CancellationToken ct = default)
    {
        var serviceTypes = await _db.ServiceTypes
            .Where(st => request.ServiceTypeIds.Contains(st.Id))
            .ToListAsync(ct);

        if (serviceTypes.Count != request.ServiceTypeIds.Count)
            return (false, "One or more service types not found.", null);

        // Step 1: total duration including mandatory buffer
        int totalMinutes = serviceTypes.Sum(st => st.DefaultDurationMinutes) + BufferMinutes;
        var endTime = request.StartTime.AddMinutes(totalMinutes);

        _logger.LogInformation(
            "Booking attempt: location={Location} start={Start} end={End} serviceTypes=[{ServiceTypes}]",
            request.DealershipLocation, request.StartTime, endTime,
            string.Join(",", request.ServiceTypeIds));

        // Availability checks run inside the serializable transaction to eliminate the TOCTOU race
        // Switch to BeginTransactionAsync(IsolationLevel.Serializable, ct) when targeting a relational DB.
        using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var bay = await FindAvailableBayAsync(request.DealershipLocation, serviceTypes, request.StartTime, endTime, ct);
            if (bay is null)
            {
                await tx.RollbackAsync(ct);
                _logger.LogWarning(
                    "No available bay: location={Location} start={Start}",
                    request.DealershipLocation, request.StartTime);
                return (false, "No available service bay for the requested time window.", null);
            }

            var technician = await FindAvailableTechnicianAsync(request.DealershipLocation, serviceTypes, request.StartTime, endTime, ct);
            if (technician is null)
            {
                await tx.RollbackAsync(ct);
                _logger.LogWarning(
                    "No available technician: location={Location} start={Start}",
                    request.DealershipLocation, request.StartTime);
                return (false, "No available technician for the requested time window.", null);
            }

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
            await _db.SaveChangesAsync(ct);

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

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Appointment confirmed: id={AppointmentId} bay={BayId} technician={TechnicianId}",
                appointment.Id, bay.Id, technician.Id);

            return (true, string.Empty, appointment);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex,
                "Booking failed: location={Location} start={Start}",
                request.DealershipLocation, request.StartTime);
            throw;
        }
    }

    public async Task<(bool Success, string Error)> CancelAppointmentAsync(
        int appointmentId, string cancelledBy, string reason, CancellationToken ct = default)
    {
        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
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

        await _db.SaveChangesAsync(ct);

        // Cancelled appointments are excluded from collision checks — resources are immediately freed
        _logger.LogInformation(
            "Appointment cancelled: id={AppointmentId} from={FromStatus} by={CancelledBy}",
            appointmentId, fromStatus, cancelledBy);

        return (true, string.Empty);
    }

    // Overlap condition: existingStart < requestedEnd && existingEnd > requestedStart
    private async Task<ServiceBay?> FindAvailableBayAsync(
        string location, List<ServiceType> serviceTypes, DateTime start, DateTime end, CancellationToken ct)
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
            .ToListAsync(ct);

        return await _db.ServiceBays
            .Where(b => b.DealershipLocation == location
                     && b.IsActive
                     && b.CapabilityTag >= minCapability
                     && !busyBayIds.Contains(b.Id))
            .FirstOrDefaultAsync(ct);
    }

    private async Task<Technician?> FindAvailableTechnicianAsync(
        string location, List<ServiceType> serviceTypes, DateTime start, DateTime end, CancellationToken ct)
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
            .ToListAsync(ct);

        return await _db.Technicians
            .Where(t => t.DealershipLocation == location
                     && t.IsActive
                     && t.Skill >= minSkill
                     && !busyTechIds.Contains(t.Id))
            .FirstOrDefaultAsync(ct);
    }
}
