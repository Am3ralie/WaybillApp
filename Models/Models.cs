using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WaybillApp.Models;

// ─── Пользователи ───────────────────────────────────────────────────────────

public class ApplicationUser : IdentityUser
{
    [Required, Display(Name = "ФИО")]
    public string FullName { get; set; } = "";

    [Display(Name = "Роль")]
    public string Role { get; set; } = "operator"; // admin | driver | operator
}

// ─── Водители ────────────────────────────────────────────────────────────────

public class Driver
{
    public int Id { get; set; }

    [Required, Display(Name = "ФИО")]
    public string FullName { get; set; } = "";

    [Display(Name = "Табельный номер")]
    public string? TabNumber { get; set; }

    [Display(Name = "Номер удостоверения")]
    public string? LicenseNumber { get; set; }

    [Display(Name = "Срок действия прав")]
    public DateOnly? LicenseExpiry { get; set; }

    [Display(Name = "Телефон")]
    public string? Phone { get; set; }

    [Display(Name = "Активен")]
    public bool IsActive { get; set; } = true;

    public ICollection<Waybill> Waybills { get; set; } = new List<Waybill>();
}

// ─── Транспортные средства ────────────────────────────────────────────────────

public class Vehicle
{
    public int Id { get; set; }

    [Required, Display(Name = "Гос. номер")]
    public string PlateNumber { get; set; } = "";

    [Display(Name = "Марка/Модель")]
    public string? Model { get; set; }

    [Display(Name = "VIN")]
    public string? Vin { get; set; }

    [Display(Name = "Техосмотр до")]
    public DateOnly? TechInspectionExpiry { get; set; }

    [Display(Name = "Активен")]
    public bool IsActive { get; set; } = true;

    public ICollection<Waybill> Waybills { get; set; } = new List<Waybill>();
}

// ─── Путевые листы ────────────────────────────────────────────────────────────

public class Waybill
{
    public int Id { get; set; }

    [Display(Name = "Номер листа")]
    public string? Number { get; set; }

    [Required, Display(Name = "Водитель")]
    public int DriverId { get; set; }
    public Driver? Driver { get; set; }

    [Required, Display(Name = "ТС")]
    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    [Display(Name = "Дата")]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Время выезда")]
    public TimeOnly? DepartureTime { get; set; }

    [Display(Name = "Время возвращения")]
    public TimeOnly? ArrivalTime { get; set; }

    /// <summary>Время в наряде (ч) = ArrivalTime - DepartureTime</summary>
    [NotMapped]
    public double? TimeInDuty =>
        (DepartureTime.HasValue && ArrivalTime.HasValue)
        ? (ArrivalTime.Value.ToTimeSpan() - DepartureTime.Value.ToTimeSpan()).TotalHours
        : null;

    [Display(Name = "Спидометр при выезде (км)")]
    public int? OdometerStart { get; set; }

    [Display(Name = "Спидометр при возвращении (км)")]
    public int? OdometerEnd { get; set; }

    [NotMapped]
    public int? DailyMileage => (OdometerStart.HasValue && OdometerEnd.HasValue)
        ? OdometerEnd - OdometerStart : null;

    [Display(Name = "Остаток топлива при выезде (л)")]
    public double? FuelAtDeparture { get; set; }

    [Display(Name = "Остаток топлива при возвращении (л)")]
    public double? FuelAtArrival { get; set; }

    [Display(Name = "Марка топлива")]
    public string? FuelType { get; set; }

    [Display(Name = "Заправлено (л)")]
    public double? FuelAdded { get; set; }

    [Display(Name = "Маршрут")]
    public string? Route { get; set; }

    [Display(Name = "Вне города")]
    public bool IsOutOfCity { get; set; } = false;

    [Display(Name = "Зимний период")]
    public bool IsWinter { get; set; } = false;

    [Display(Name = "С грузом")]
    public bool WithCargo { get; set; } = false;

    [Display(Name = "Статус")]
    public string Status { get; set; } = "open"; // open | closed | printed

    [Display(Name = "Примечание")]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }

    // ─── Расчёт нормы расхода ────────────────────────────────────────────────

    /// <summary>Расход по норме (л) на основе коэффициентов из FuelNorms</summary>
    [NotMapped]
    public double? FuelByNorm { get; set; } // вычисляется в контроллере

    /// <summary>Фактический расход = остаток_выезд + заправлено - остаток_возврат</summary>
    [NotMapped]
    public double? FuelActual =>
        (FuelAtDeparture.HasValue && FuelAdded.HasValue && FuelAtArrival.HasValue)
        ? FuelAtDeparture + FuelAdded - FuelAtArrival : null;

    [NotMapped]
    public double? FuelEconomyOrOverrun =>
        (FuelByNorm.HasValue && FuelActual.HasValue)
        ? FuelByNorm - FuelActual : null;
}

// ─── Нормы расхода топлива (редактирует оператор) ────────────────────────────

public class FuelNorm
{
    public int Id { get; set; }

    [Required, Display(Name = "Название")]
    public string Name { get; set; } = "Основная норма";

    [Display(Name = "Базовая норма (л/100 км)")]
    public double BaseNorm { get; set; } = 23.8;

    [Display(Name = "Коэф. на груз")]
    public double KCargo { get; set; } = 0.05;

    [Display(Name = "Коэф. на город")]
    public double KCity { get; set; } = 0.10;

    [Display(Name = "Коэф. на зиму")]
    public double KWinter { get; set; } = 0.20;

    [Display(Name = "Действует с")]
    public DateOnly EffectiveFrom { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Примечание")]
    public string? Notes { get; set; }

    // ─── Расчётные нормы ─────────────────────────────────────────────────────

    /// <summary>Летняя, без груза, за городом</summary>
    [NotMapped] public double SummerNoCargo => BaseNorm;

    /// <summary>Летняя, с грузом, за городом</summary>
    [NotMapped] public double SummerWithCargo => BaseNorm * (1 + KCargo);

    /// <summary>Зимняя, без груза, за городом</summary>
    [NotMapped] public double WinterNoCargo => BaseNorm * (1 + KWinter);

    /// <summary>Зимняя, с грузом, за городом</summary>
    [NotMapped] public double WinterWithCargo => BaseNorm * (1 + KCargo) * (1 + KWinter);

    /// <summary>Летняя, без груза, по городу</summary>
    [NotMapped] public double SummerNoCargoCity => BaseNorm * (1 + KCity);

    /// <summary>Летняя, с грузом, по городу</summary>
    [NotMapped] public double SummerWithCargoCity => BaseNorm * (1 + KCargo) * (1 + KCity);

    /// <summary>Зимняя, без груза, по городу</summary>
    [NotMapped] public double WinterNoCargoCity => BaseNorm * (1 + KWinter) * (1 + KCity);

    /// <summary>Зимняя, с грузом, по городу</summary>
    [NotMapped] public double WinterWithCargoCity => BaseNorm * (1 + KCargo) * (1 + KWinter) * (1 + KCity);

    /// <summary>Получить актуальную норму для условий</summary>
    public double GetNorm(bool isWinter, bool withCargo, bool outOfCity)
    {
        return (isWinter, withCargo, outOfCity) switch
        {
            (false, false, true)  => SummerNoCargo,
            (false, true,  true)  => SummerWithCargo,
            (true,  false, true)  => WinterNoCargo,
            (true,  true,  true)  => WinterWithCargo,
            (false, false, false) => SummerNoCargoCity,
            (false, true,  false) => SummerWithCargoCity,
            (true,  false, false) => WinterNoCargoCity,
            (true,  true,  false) => WinterWithCargoCity,
        };
    }
}
