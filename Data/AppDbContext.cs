using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WaybillApp.Models;

namespace WaybillApp.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Waybill> Waybills => Set<Waybill>();
    public DbSet<FuelNorm> FuelNorms => Set<FuelNorm>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Waybill>()
            .HasOne(w => w.Driver)
            .WithMany(d => d.Waybills)
            .HasForeignKey(w => w.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Waybill>()
            .HasOne(w => w.Vehicle)
            .WithMany(v => v.Waybills)
            .HasForeignKey(w => w.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Стартовая норма
        builder.Entity<FuelNorm>().HasData(new FuelNorm
        {
            Id = 1,
            Name = "Основная норма",
            BaseNorm = 23.8,
            KCargo = 0.05,
            KCity = 0.10,
            KWinter = 0.20,
            EffectiveFrom = new DateOnly(2024, 1, 1)
        });
    }
}
