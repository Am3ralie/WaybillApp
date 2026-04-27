using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaybillApp.Data;
using WaybillApp.Models;
using WaybillApp.Services;

namespace WaybillApp.Controllers;

// ─── Водители ────────────────────────────────────────────────────────────────

[Authorize]
public class DriversController : Controller
{
    private readonly AppDbContext _db;
    public DriversController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index() =>
        View(await _db.Drivers.OrderBy(d => d.FullName).ToListAsync());

    [Authorize(Roles = "admin,operator")]
    public IActionResult Create() => View(new Driver());

    [Authorize(Roles = "admin,operator"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Driver d)
    {
        if (!ModelState.IsValid) return View(d);
        _db.Drivers.Add(d);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Водитель «{d.FullName}» добавлен.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> Edit(int id)
    {
        var d = await _db.Drivers.FindAsync(id);
        return d == null ? NotFound() : View(d);
    }

    [Authorize(Roles = "admin,operator"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Driver d)
    {
        if (id != d.Id) return BadRequest();
        if (!ModelState.IsValid) return View(d);
        _db.Update(d);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Данные водителя обновлены.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var d = await _db.Drivers.FindAsync(id);
        if (d != null) { _db.Drivers.Remove(d); await _db.SaveChangesAsync(); }
        TempData["Success"] = "Водитель удалён.";
        return RedirectToAction(nameof(Index));
    }
}

// ─── Транспортные средства ────────────────────────────────────────────────────

[Authorize]
public class VehiclesController : Controller
{
    private readonly AppDbContext _db;
    public VehiclesController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index() =>
        View(await _db.Vehicles.OrderBy(v => v.PlateNumber).ToListAsync());

    [Authorize(Roles = "admin,operator")]
    public IActionResult Create() => View(new Vehicle());

    [Authorize(Roles = "admin,operator"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Vehicle v)
    {
        if (!ModelState.IsValid) return View(v);
        _db.Vehicles.Add(v);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"ТС «{v.PlateNumber}» добавлено.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> Edit(int id)
    {
        var v = await _db.Vehicles.FindAsync(id);
        return v == null ? NotFound() : View(v);
    }

    [Authorize(Roles = "admin,operator"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Vehicle v)
    {
        if (id != v.Id) return BadRequest();
        if (!ModelState.IsValid) return View(v);
        _db.Update(v);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Данные ТС обновлены.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var v = await _db.Vehicles.FindAsync(id);
        if (v != null) { _db.Vehicles.Remove(v); await _db.SaveChangesAsync(); }
        TempData["Success"] = "ТС удалено.";
        return RedirectToAction(nameof(Index));
    }
}

// ─── Нормы расхода ────────────────────────────────────────────────────────────

[Authorize]
public class FuelNormsController : Controller
{
    private readonly AppDbContext   _db;
    private readonly WaybillService _svc;

    public FuelNormsController(AppDbContext db, WaybillService svc)
    {
        _db  = db;
        _svc = svc;
    }

    public async Task<IActionResult> Index() =>
        View(await _db.FuelNorms.OrderByDescending(n => n.EffectiveFrom).ToListAsync());

    [Authorize(Roles = "admin,operator")]
    public IActionResult Create()
    {
        // Предзаполняем значениями текущей нормы, чтобы не вбивать всё заново
        var latest = _svc.GetLatestNorm();
        return View(new FuelNorm
        {
            BaseNorm      = latest.BaseNorm,
            KCargo        = latest.KCargo,
            KCity         = latest.KCity,
            KWinter       = latest.KWinter,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.Today),
        });
    }

    [Authorize(Roles = "admin,operator"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FuelNorm n)
    {
        if (!ModelState.IsValid) return View(n);
        _db.FuelNorms.Add(n);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Норма «{n.Name}» добавлена и будет применяться с {n.EffectiveFrom:dd.MM.yyyy}.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> Edit(int id)
    {
        var n = await _db.FuelNorms.FindAsync(id);
        return n == null ? NotFound() : View(n);
    }

    [Authorize(Roles = "admin,operator"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FuelNorm n)
    {
        if (id != n.Id) return BadRequest();
        if (!ModelState.IsValid) return View(n);
        _db.Update(n);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Норма обновлена.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (await _db.FuelNorms.CountAsync() <= 1)
        {
            TempData["Error"] = "Нельзя удалить единственную норму — она используется для расчётов.";
            return RedirectToAction(nameof(Index));
        }
        var n = await _db.FuelNorms.FindAsync(id);
        if (n != null) { _db.FuelNorms.Remove(n); await _db.SaveChangesAsync(); }
        TempData["Success"] = "Норма удалена.";
        return RedirectToAction(nameof(Index));
    }
}
