using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServiceScheduler.Api.Data;
using ServiceScheduler.Api.Models;
using ServiceScheduler.Api.Options;
using ServiceScheduler.Api.Services;

namespace ServiceScheduler.Tests;

public class SchedulingServiceTests
{
    private static SchedulerDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SchedulerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // isolated per test
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new SchedulerDbContext(options);
    }

    private static SchedulingService CreateService(SchedulerDbContext db) =>
        new(db, NullLogger<SchedulingService>.Instance, Options.Create(new SchedulingOptions()));

    private static async Task SeedAsync(SchedulerDbContext db)
    {
        db.ServiceTypes.Add(new ServiceType
        {
            Id = 1,
            Name = "Oil Change",
            DefaultDurationMinutes = 30,
            RequiredSkill = TechnicianSkill.General,
            RequiredBayCapability = BayCapabilityTag.General
        });
        db.ServiceBays.Add(new ServiceBay
        {
            Id = 1,
            Name = "Bay 1",
            DealershipLocation = "Main",
            CapabilityTag = BayCapabilityTag.General,
            IsActive = true
        });
        db.Technicians.Add(new Technician
        {
            Id = 1,
            Name = "John Smith",
            DealershipLocation = "Main",
            Skill = TechnicianSkill.General,
            IsActive = true,
            ShiftStart = new TimeOnly(8, 0),
            ShiftEnd = new TimeOnly(17, 0)
        });
        db.Customers.Add(new Customer { Id = 1, Name = "Alice", Email = "alice@test.com", Phone = "555-0001" });
        db.Vehicles.Add(new Vehicle
        {
            Id = 1,
            Vin = "1HGBH41JXMN109186",
            Make = "Honda",
            Model = "Civic",
            Year = 2021,
            CustomerId = 1
        });
        await db.SaveChangesAsync();
    }

    private static BookAppointmentRequest MakeRequest(DateTime start) => new()
    {
        CustomerId = 1,
        VehicleId = 1,
        DealershipLocation = "Main",
        ServiceTypeIds = new List<int> { 1 },
        StartTime = start,
        AdvisorId = "advisor1"
    };

    [Fact]
    public async Task Book_ValidRequest_ReturnsConfirmedAppointment()
    {
        var db = CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);

        var (success, _, appointment) = await service.BookAppointmentAsync(MakeRequest(DateTime.UtcNow.AddDays(1)));

        Assert.True(success);
        Assert.NotNull(appointment);
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        Assert.NotNull(appointment.Customer);
        Assert.NotNull(appointment.Vehicle);
        Assert.NotNull(appointment.ServiceBay);
        Assert.NotNull(appointment.Technician);
        Assert.NotEmpty(appointment.ServiceLines);
        Assert.All(appointment.ServiceLines, sl => Assert.NotNull(sl.ServiceType));
    }

    [Fact]
    public async Task Book_BayAndTechAlreadyBooked_ReturnsConflictError()
    {
        var db = CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);
        var start = DateTime.UtcNow.AddDays(1);

        await service.BookAppointmentAsync(MakeRequest(start)); // occupies the only bay and tech

        var (success, error, _) = await service.BookAppointmentAsync(MakeRequest(start));

        Assert.False(success);
        Assert.NotEmpty(error);
    }

    [Fact]
    public async Task Cancel_ReleasesResources_AllowsSubsequentBookingAtSameSlot()
    {
        var db = CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);
        var start = DateTime.UtcNow.AddDays(1);

        var (_, _, first) = await service.BookAppointmentAsync(MakeRequest(start));
        await service.CancelAppointmentAsync(first!.Id, "advisor1", "Customer no-show");

        var (success, _, _) = await service.BookAppointmentAsync(MakeRequest(start));

        Assert.True(success); // resource freed by cancellation, same slot must be bookable again
    }

    [Fact]
    public async Task Transition_ConfirmedToInProgress_Succeeds()
    {
        var db = CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);

        var (_, _, appt) = await service.BookAppointmentAsync(MakeRequest(DateTime.UtcNow.AddDays(1)));
        var (success, error) = await service.TransitionAppointmentAsync(appt!.Id, AppointmentStatus.InProgress, "advisor1", "Vehicle checked in");

        Assert.True(success);
        Assert.Empty(error);
        Assert.Equal(AppointmentStatus.InProgress, (await db.Appointments.FindAsync(appt.Id))!.Status);
    }

    [Fact]
    public async Task Transition_InProgressToCompleted_Succeeds()
    {
        var db = CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);

        var (_, _, appt) = await service.BookAppointmentAsync(MakeRequest(DateTime.UtcNow.AddDays(1)));
        await service.TransitionAppointmentAsync(appt!.Id, AppointmentStatus.InProgress, "advisor1", "Started");
        var (success, error) = await service.TransitionAppointmentAsync(appt.Id, AppointmentStatus.Completed, "advisor1", "Done");

        Assert.True(success);
        Assert.Empty(error);
        Assert.Equal(AppointmentStatus.Completed, (await db.Appointments.FindAsync(appt.Id))!.Status);
    }

    [Fact]
    public async Task Transition_SkippingInProgress_ReturnsError()
    {
        var db = CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);

        var (_, _, appt) = await service.BookAppointmentAsync(MakeRequest(DateTime.UtcNow.AddDays(1)));
        var (success, error) = await service.TransitionAppointmentAsync(appt!.Id, AppointmentStatus.Completed, "advisor1", "Skip");

        Assert.False(success); // Confirmed → Completed is not a valid transition
        Assert.NotEmpty(error);
    }

    [Fact]
    public async Task Transition_CompletedAppointment_CannotBeCancelled()
    {
        var db = CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);

        var (_, _, appt) = await service.BookAppointmentAsync(MakeRequest(DateTime.UtcNow.AddDays(1)));
        await service.TransitionAppointmentAsync(appt!.Id, AppointmentStatus.InProgress, "advisor1", "Started");
        await service.TransitionAppointmentAsync(appt.Id, AppointmentStatus.Completed, "advisor1", "Done");

        var (success, error) = await service.CancelAppointmentAsync(appt.Id, "advisor1", "Late cancel");

        Assert.False(success); // Completed is a terminal state
        Assert.NotEmpty(error);
    }

    [Fact]
    public async Task Transition_AuditLog_RecordsEveryStateChange()
    {
        var db = CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);

        var (_, _, appt) = await service.BookAppointmentAsync(MakeRequest(DateTime.UtcNow.AddDays(1)));
        await service.TransitionAppointmentAsync(appt!.Id, AppointmentStatus.InProgress, "advisor1", "Started");
        await service.TransitionAppointmentAsync(appt.Id, AppointmentStatus.Completed, "advisor1", "Done");

        var logs = db.AppointmentAuditLogs
            .Where(l => l.AppointmentId == appt.Id)
            .OrderBy(l => l.ChangedAt)
            .ToList();

        Assert.Equal(3, logs.Count); // Book + Start + Complete each write one log entry
        Assert.Equal(AppointmentStatus.Confirmed,  logs[0].ToStatus);
        Assert.Equal(AppointmentStatus.InProgress, logs[1].ToStatus);
        Assert.Equal(AppointmentStatus.Completed,  logs[2].ToStatus);
    }

    [Fact]
    public async Task Transition_NonExistentAppointment_ReturnsFalse()
    {
        var db = CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);

        var (success, error) = await service.TransitionAppointmentAsync(9999, AppointmentStatus.InProgress, "advisor1", "test");

        Assert.False(success);
        Assert.NotEmpty(error);
    }
}
