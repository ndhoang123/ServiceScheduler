using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceScheduler.Api.Data;
using ServiceScheduler.Api.Models;

namespace ServiceScheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private readonly SchedulerDbContext _db;

    public SeedController(SchedulerDbContext db) => _db = db;

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Seed()
    {
        if (_db.ServiceBays.Any())
            return Conflict(new { message = "Database is already seeded." });

        _db.ServiceTypes.AddRange(
            new ServiceType { Id = 1, Name = "Oil Change",        DefaultDurationMinutes = 30, RequiredSkill = TechnicianSkill.General,    RequiredBayCapability = BayCapabilityTag.General },
            new ServiceType { Id = 2, Name = "Brake Repair",      DefaultDurationMinutes = 60, RequiredSkill = TechnicianSkill.HeavyRepair, RequiredBayCapability = BayCapabilityTag.HeavyRepair },
            new ServiceType { Id = 3, Name = "EV Battery Check",  DefaultDurationMinutes = 45, RequiredSkill = TechnicianSkill.EvCertified, RequiredBayCapability = BayCapabilityTag.EvCertified }
        );

        _db.ServiceBays.AddRange(
            new ServiceBay { Id = 1, Name = "Bay 1 – General",   DealershipLocation = "Main", CapabilityTag = BayCapabilityTag.General,    IsActive = true },
            new ServiceBay { Id = 2, Name = "Bay 2 – Heavy",     DealershipLocation = "Main", CapabilityTag = BayCapabilityTag.HeavyRepair, IsActive = true },
            new ServiceBay { Id = 3, Name = "Bay 3 – EV",        DealershipLocation = "Main", CapabilityTag = BayCapabilityTag.EvCertified, IsActive = true }
        );

        _db.Technicians.AddRange(
            new Technician { Id = 1, Name = "Alice Nguyen",  DealershipLocation = "Main", Skill = TechnicianSkill.General,    IsActive = true, ShiftStart = new TimeOnly(8, 0),  ShiftEnd = new TimeOnly(17, 0) },
            new Technician { Id = 2, Name = "Bob Martinez",  DealershipLocation = "Main", Skill = TechnicianSkill.HeavyRepair, IsActive = true, ShiftStart = new TimeOnly(8, 0),  ShiftEnd = new TimeOnly(17, 0) },
            new Technician { Id = 3, Name = "Carol Kim",     DealershipLocation = "Main", Skill = TechnicianSkill.EvCertified, IsActive = true, ShiftStart = new TimeOnly(9, 0),  ShiftEnd = new TimeOnly(18, 0) }
        );

        _db.Customers.AddRange(
            new Customer { Id = 1, Name = "James Walker",  Email = "james@example.com",  Phone = "555-0101" },
            new Customer { Id = 2, Name = "Sarah Chen",    Email = "sarah@example.com",  Phone = "555-0102" }
        );

        _db.Vehicles.AddRange(
            new Vehicle { Id = 1, Vin = "1HGBH41JXMN109186", Make = "Honda",  Model = "Civic",    Year = 2021, CustomerId = 1 },
            new Vehicle { Id = 2, Vin = "5YJSA1DN5DFP14705", Make = "Tesla",  Model = "Model S",  Year = 2023, CustomerId = 2 }
        );

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Seed complete.",
            serviceTypes  = new[] { "Oil Change (id:1)", "Brake Repair (id:2)", "EV Battery Check (id:3)" },
            serviceBays   = new[] { "Bay 1 – General (id:1)", "Bay 2 – Heavy (id:2)", "Bay 3 – EV (id:3)" },
            technicians   = new[] { "Alice Nguyen – General (id:1)", "Bob Martinez – HeavyRepair (id:2)", "Carol Kim – EvCertified (id:3)" },
            customers     = new[] { "James Walker (id:1)", "Sarah Chen (id:2)" },
            vehicles      = new[] { "Honda Civic VIN:1HGBH41JXMN109186 (id:1)", "Tesla Model S VIN:5YJSA1DN5DFP14705 (id:2)" }
        });
    }
}
