using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceScheduler.Api.Data;
using ServiceScheduler.Api.Models;
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
        var service = new SchedulingService(db, NullLogger<SchedulingService>.Instance);

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
        var service = new SchedulingService(db, NullLogger<SchedulingService>.Instance);
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
        var service = new SchedulingService(db, NullLogger<SchedulingService>.Instance);
        var start = DateTime.UtcNow.AddDays(1);

        var (_, _, first) = await service.BookAppointmentAsync(MakeRequest(start));
        await service.CancelAppointmentAsync(first!.Id, "advisor1", "Customer no-show");

        var (success, _, _) = await service.BookAppointmentAsync(MakeRequest(start));

        Assert.True(success); // resource freed by cancellation, same slot must be bookable again
    }
}
