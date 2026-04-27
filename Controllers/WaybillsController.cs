using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaybillApp.Data;
using WaybillApp.Models;
using WaybillApp.Services;

namespace WaybillApp.Controllers;

[Authorize]
public class WaybillsController : Controller
{
    private readonly AppDbContext   _db;
    private readonly WaybillService _svc;

    public WaybillsController(AppDbContext db, WaybillService svc)
    {
        _db  = db;
        _svc = svc;
    }

    // GET /Waybills
    public async Task<IActionResult> Index(string? search, int? month, int? year)
    {
        string? driverFilter = User.IsInRole("driver") ? User.Identity!.Name : null;
        var list = await _svc.GetFilteredAsync(search, month, year, driverFilter);
        ViewBag.Search = search;
        ViewBag.Month  = month;
        ViewBag.Year   = year;
        ViewBag.Norm   = _svc.GetLatestNorm();
        return View(list);
    }

    // GET /Waybills/Create
    [Authorize(Roles = "admin,operator")]
    public IActionResult Create()
    {
        PopulateDropdowns();
        ViewBag.Norm = _svc.GetLatestNorm();
        return View(new Waybill { Date = DateOnly.FromDateTime(DateTime.Today) });
    }

    // POST /Waybills/Create
    [Authorize(Roles = "admin,operator"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Waybill w)
    {
        ModelState.Remove(nameof(Waybill.Driver));
        ModelState.Remove(nameof(Waybill.Vehicle));
        if (!ModelState.IsValid) { PopulateDropdowns(); ViewBag.Norm = _svc.GetLatestNorm(); return View(w); }
        await _svc.CreateAsync(w, User.Identity?.Name);
        TempData["Success"] = $"Путевой лист «{w.Number ?? "#" + w.Id}» создан.";
        return RedirectToAction(nameof(Index));
    }

    // GET /Waybills/Edit/5
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> Edit(int id)
    {
        var w = await _db.Waybills.FindAsync(id);
        if (w == null) return NotFound();
        PopulateDropdowns();
        ViewBag.Norm = _svc.GetNormForDate(w.Date);
        return View(w);
    }

    // POST /Waybills/Edit/5
    [Authorize(Roles = "admin,operator"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Waybill w)
    {
        if (id != w.Id) return BadRequest();
        ModelState.Remove(nameof(Waybill.Driver));
        ModelState.Remove(nameof(Waybill.Vehicle));
        if (!ModelState.IsValid) { PopulateDropdowns(); ViewBag.Norm = _svc.GetNormForDate(w.Date); return View(w); }
        await _svc.UpdateAsync(w);
        TempData["Success"] = "Путевой лист обновлён.";
        return RedirectToAction(nameof(Index));
    }

    // GET /Waybills/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var w = await _svc.GetByIdAsync(id);
        if (w == null) return NotFound();
        ViewBag.Norm = _svc.GetNormForDate(w.Date);
        return View(w);
    }

    // POST /Waybills/Delete/5
    [Authorize(Roles = "admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _svc.DeleteAsync(id);
        TempData["Success"] = "Путевой лист удалён.";
        return RedirectToAction(nameof(Index));
    }

    // GET /Waybills/Print/5
    public async Task<IActionResult> Print(int id)
    {
        var w = await _svc.GetByIdAsync(id);
        if (w == null) return NotFound();
        ViewBag.Norm = _svc.GetNormForDate(w.Date);
        return View(w);
    }

    // GET /Waybills/ExportExcel
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> ExportExcel(int? month, int? year)
    {
        var bytes = await _svc.ExportExcelAsync(month, year);
        var period = year.HasValue
            ? (month.HasValue ? $"{year}_{month:D2}" : year.ToString()!)
            : DateTime.Now.ToString("yyyy");
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"waybills_{period}.xlsx");
    }

    private void PopulateDropdowns()
    {
        ViewBag.Drivers  = _db.Drivers.Where(d => d.IsActive).OrderBy(d => d.FullName).ToList();
        ViewBag.Vehicles = _db.Vehicles.Where(v => v.IsActive).OrderBy(v => v.PlateNumber).ToList();
    }
}
