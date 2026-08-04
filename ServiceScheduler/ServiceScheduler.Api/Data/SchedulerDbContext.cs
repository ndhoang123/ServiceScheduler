using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Api.Models;

namespace ServiceScheduler.Api.Data;

public class SchedulerDbContext : DbContext
{
    public SchedulerDbContext(DbContextOptions<SchedulerDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<ServiceBay> ServiceBays => Set<ServiceBay>();
    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentServiceLine> AppointmentServiceLines => Set<AppointmentServiceLine>();
    public DbSet<AppointmentAuditLog> AppointmentAuditLogs => Set<AppointmentAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Fast VIN lookups
        modelBuilder.Entity<Vehicle>()
            .HasIndex(v => v.Vin)
            .IsUnique();

        // Fast time-window collision checks per bay and per technician
        modelBuilder.Entity<Appointment>()
            .HasIndex(a => new { a.ServiceBayId, a.StartTime, a.EndTime });

        modelBuilder.Entity<Appointment>()
            .HasIndex(a => new { a.TechnicianId, a.StartTime, a.EndTime });

        modelBuilder.Entity<Appointment>()
            .HasIndex(a => new { a.DealershipLocation, a.StartTime });
    }
}
