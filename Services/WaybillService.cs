using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using WaybillApp.Data;
using WaybillApp.Models;

namespace WaybillApp.Services;

/// <summary>
/// Центральный сервис: расчёт норм расхода, применение к путевым листам,
/// экспорт в Excel и формирование данных для печати.
///
/// Коэффициенты берутся из последней записи FuelNorm (таблица БД),
/// поэтому оператор может менять их через веб-интерфейс — без перезапуска.
/// </summary>
public class WaybillService
{
    private readonly AppDbContext _db;

    public WaybillService(AppDbContext db) => _db = db;

    // =========================================================================
    //  НОРМЫ РАСХОДА
    // =========================================================================

    /// <summary>Возвращает норму, действующую на указанную дату (или последнюю).</summary>
    public FuelNorm GetNormForDate(DateOnly date)
    {
        // Берём норму, действующую на дату листа (EffectiveFrom <= date),
        // или просто последнюю если ни одна не подходит.
        return _db.FuelNorms
            .Where(n => n.EffectiveFrom <= date)
            .OrderByDescending(n => n.EffectiveFrom)
            .FirstOrDefault()
            ?? _db.FuelNorms.OrderByDescending(n => n.EffectiveFrom).First();
    }

    /// <summary>Возвращает самую последнюю норму (для форм создания).</summary>
    public FuelNorm GetLatestNorm() =>
        _db.FuelNorms.OrderByDescending(n => n.EffectiveFrom).First();

    /// <summary>
    /// Рассчитывает норму расхода на 100 км по формуле:
    ///   base × (1 + k_cargo?) × (1 + k_winter?) × (1 + k_city?)
    /// Результат совпадает с расчётными полями модели FuelNorm.
    /// </summary>
    public static double CalcRate(FuelNorm norm, bool isWinter, bool withCargo, bool outOfCity)
    {
        double rate = norm.BaseNorm;
        if (withCargo) rate *= 1 + norm.KCargo;
        if (isWinter)  rate *= 1 + norm.KWinter;
        if (!outOfCity) rate *= 1 + norm.KCity;   // !outOfCity → по городу
        return rate;
    }

    /// <summary>
    /// Рассчитывает расход по норме для одного путевого листа (л).
    /// Формула: пробег_км × ставка / 100.
    /// Записывает результат в <see cref="Waybill.FuelByNorm"/>.
    /// </summary>
    public void ApplyNorm(Waybill w)
    {
        if (!w.DailyMileage.HasValue) return;
        var norm = GetNormForDate(w.Date);
        var rate = CalcRate(norm, w.IsWinter, w.WithCargo, w.IsOutOfCity);
        w.FuelByNorm = Math.Round(w.DailyMileage.Value * rate / 100.0, 2);
    }

    /// <summary>Применяет нормы ко всем листам в списке.</summary>
    public void ApplyNorms(IEnumerable<Waybill> waybills)
    {
        foreach (var w in waybills) ApplyNorm(w);
    }

    // =========================================================================
    //  ВЫБОРКА ДАННЫХ
    // =========================================================================

    /// <summary>Возвращает список путевых листов с фильтрацией и уже рассчитанными нормами.</summary>
    public async Task<List<Waybill>> GetFilteredAsync(
        string? search, int? month, int? year, string? driverId = null)
    {
        var q = _db.Waybills
            .Include(w => w.Driver)
            .Include(w => w.Vehicle)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(w =>
                (w.Number != null && w.Number.Contains(s)) ||
                w.Driver!.FullName.Contains(s) ||
                w.Vehicle!.PlateNumber.Contains(s) ||
                (w.Route != null && w.Route.Contains(s)));
        }

        if (month.HasValue) q = q.Where(w => w.Date.Month == month.Value);
        if (year.HasValue)  q = q.Where(w => w.Date.Year  == year.Value);
        if (driverId != null) q = q.Where(w => w.CreatedByUserId == driverId);

        var list = await q.OrderByDescending(w => w.Date).ThenBy(w => w.Id).ToListAsync();
        ApplyNorms(list);
        return list;
    }

    /// <summary>Загружает один лист с навигационными свойствами и применяет норму.</summary>
    public async Task<Waybill?> GetByIdAsync(int id)
    {
        var w = await _db.Waybills
            .Include(x => x.Driver)
            .Include(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (w != null) ApplyNorm(w);
        return w;
    }

    // =========================================================================
    //  СОХРАНЕНИЕ
    // =========================================================================

    public async Task CreateAsync(Waybill w, string? createdByUser)
    {
        w.CreatedAt       = DateTime.UtcNow;
        w.CreatedByUserId = createdByUser;
        _db.Waybills.Add(w);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Waybill w)
    {
        _db.Waybills.Update(w);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var w = await _db.Waybills.FindAsync(id);
        if (w == null) return false;
        _db.Waybills.Remove(w);
        await _db.SaveChangesAsync();
        return true;
    }

    // =========================================================================
    //  СТАТИСТИКА ДЛЯ ГЛАВНОЙ СТРАНИЦЫ
    // =========================================================================

    public async Task<DashboardStats> GetStatsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var waybills = await _db.Waybills.ToListAsync();
        ApplyNorms(waybills);

        return new DashboardStats
        {
            TotalWaybills   = waybills.Count,
            TodayCount      = waybills.Count(w => w.Date == today),
            MonthCount      = waybills.Count(w => w.Date >= monthStart),
            ActiveDrivers   = await _db.Drivers.CountAsync(d => d.IsActive),
            ActiveVehicles  = await _db.Vehicles.CountAsync(v => v.IsActive),
            MonthMileage    = waybills
                                .Where(w => w.Date >= monthStart && w.DailyMileage.HasValue)
                                .Sum(w => (long)w.DailyMileage!.Value),
            MonthFuelActual = waybills
                                .Where(w => w.Date >= monthStart && w.FuelActual.HasValue)
                                .Sum(w => w.FuelActual!.Value),
            MonthFuelNorm   = waybills
                                .Where(w => w.Date >= monthStart && w.FuelByNorm.HasValue)
                                .Sum(w => w.FuelByNorm!.Value),
        };
    }

    // =========================================================================
    //  EXCEL ЭКСПОРТ
    // =========================================================================

    /// <summary>
    /// Генерирует .xlsx с двумя листами:
    ///   1. «Путевые листы» — основная таблица с расчётами
    ///   2. «Нормы расхода» — текущие коэффициенты и расчётная таблица 4×4
    /// </summary>
    public async Task<byte[]> ExportExcelAsync(int? month, int? year)
    {
        var list = await GetFilteredAsync(null, month, year);
        var norm = GetLatestNorm();

        using var wb = new XLWorkbook();

        // ── Лист 1: путевые листы ─────────────────────────────────────────────
        var ws = wb.Worksheets.Add("Путевые листы");
        BuildWaybillSheet(ws, list, norm);

        // ── Лист 2: нормы ─────────────────────────────────────────────────────
        var wsN = wb.Worksheets.Add("Нормы расхода");
        BuildNormSheet(wsN, norm);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void BuildWaybillSheet(IXLWorksheet ws, List<Waybill> list, FuelNorm norm)
    {
        // ── Шапка ─────────────────────────────────────────────────────────────
        var colHeaders = new[]
        {
            "№ листа", "Дата",
            "Время\nвыезда", "Время\nвозврата", "Время в наряде\n(ч)",
            "Водитель", "ТС (номер)", "ТС (модель)", "Маршрут",
            "Спидометр\nвыезд (км)", "Спидометр\nвозврат (км)", "Суточный\nпробег (км)",
            "Остаток при\nвыезде (л)", "Заправлено\n(л)", "Остаток при\nвозврате (л)",
            "Марка\nтоплива",
            "Условия\n(город/загород/зима/груз)",
            "Расход\nпо норме (л)", "Факт.\nрасход (л)", "Экономия (+)\nПерерасход (-) (л)",
            "Статус", "Примечание"
        };

        for (int c = 0; c < colHeaders.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = colHeaders[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        ws.Row(1).Height = 36;

        // ── Данные ─────────────────────────────────────────────────────────────
        int row = 2;
        foreach (var w in list)
        {
            var conditions = new List<string>();
            conditions.Add(w.IsOutOfCity ? "загород" : "город");
            if (w.IsWinter) conditions.Add("зима");
            if (w.WithCargo) conditions.Add("с грузом");

            ws.Cell(row, 1).SetValue(w.Number ?? $"#{w.Id}");
            ws.Cell(row, 2).SetValue(w.Date.ToString("dd.MM.yyyy"));
            ws.Cell(row, 3).SetValue(w.DepartureTime?.ToString("HH:mm") ?? "");
            ws.Cell(row, 4).SetValue(w.ArrivalTime?.ToString("HH:mm") ?? "");
            ws.Cell(row, 5).SetValue(w.TimeInDuty.HasValue ? Math.Round(w.TimeInDuty.Value, 2) : "");
            ws.Cell(row, 6).SetValue(w.Driver?.FullName ?? "");
            ws.Cell(row, 7).SetValue(w.Vehicle?.PlateNumber ?? "");
            ws.Cell(row, 8).SetValue(w.Vehicle?.Model ?? "");
            ws.Cell(row, 9).SetValue(w.Route ?? "");
            ws.Cell(row, 10).SetValue(w.OdometerStart);
            ws.Cell(row, 11).SetValue(w.OdometerEnd);
            ws.Cell(row, 12).SetValue(w.DailyMileage);
            ws.Cell(row, 13).SetValue(w.FuelAtDeparture);
            ws.Cell(row, 14).SetValue(w.FuelAdded);
            ws.Cell(row, 15).SetValue(w.FuelAtArrival);
            ws.Cell(row, 16).SetValue(w.FuelType ?? "");
            ws.Cell(row, 17).SetValue(string.Join(", ", conditions));
            ws.Cell(row, 18).SetValue(w.FuelByNorm);
            ws.Cell(row, 19).SetValue(w.FuelActual);

            if (w.FuelEconomyOrOverrun.HasValue)
            {
                var cell = ws.Cell(row, 20);
                cell.Value = w.FuelEconomyOrOverrun.Value;
                cell.Style.Font.FontColor = w.FuelEconomyOrOverrun >= 0
                    ? XLColor.FromHtml("#166534") : XLColor.FromHtml("#991b1b");
                cell.Style.Font.Bold = true;
            }

            ws.Cell(row, 21).Value = w.Status switch
            {
                "open"    => "Открыт",
                "closed"  => "Закрыт",
                "printed" => "Распечатан",
                _         => w.Status
            };
            ws.Cell(row, 22).Value = w.Notes ?? "";

            // Зебра
            if (row % 2 == 0)
            {
                ws.Row(row).Cells(1, 22).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
            }

            // Границы строки
            ws.Row(row).Cells(1, 22).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Row(row).Cells(1, 22).Style.Border.InsideBorder  = XLBorderStyleValues.Hair;

            row++;
        }

        // ── Итоговая строка ────────────────────────────────────────────────────
        if (list.Count > 0)
        {
            var sumRow = row;
            ws.Cell(sumRow, 1).Value = "ИТОГО";
            ws.Cell(sumRow, 1).Style.Font.Bold = true;
            ws.Cell(sumRow, 12).FormulaA1 = $"=SUM(L2:L{sumRow - 1})"; // пробег
            ws.Cell(sumRow, 14).FormulaA1 = $"=SUM(N2:N{sumRow - 1})"; // заправлено
            ws.Cell(sumRow, 18).FormulaA1 = $"=SUM(R2:R{sumRow - 1})"; // по норме
            ws.Cell(sumRow, 19).FormulaA1 = $"=SUM(S2:S{sumRow - 1})"; // факт
            ws.Cell(sumRow, 20).FormulaA1 = $"=SUM(T2:T{sumRow - 1})"; // экономия

            ws.Row(sumRow).Cells(1, 22).Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");
            ws.Row(sumRow).Cells(1, 22).Style.Font.Bold = true;
            ws.Row(sumRow).Cells(1, 22).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        }

        // Ширина колонок
        ws.Columns().AdjustToContents(2, row);
        ws.Column(9).Width = 24;  // маршрут — пошире
        ws.Column(6).Width = 22;  // водитель
    }

    private static void BuildNormSheet(IXLWorksheet ws, FuelNorm norm)
    {
        var ic = System.Globalization.CultureInfo.InvariantCulture;

        // ── Коэффициенты ──────────────────────────────────────────────────────
        ws.Cell("A1").Value = $"Норма: {norm.Name}  |  Действует с: {norm.EffectiveFrom:dd.MM.yyyy}";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 13;

        var kvPairs = new (string label, string val)[]
        {
            ("Базовая норма (л / 100 км)", norm.BaseNorm.ToString("F1", ic)),
            ("Коэффициент на груз",        norm.KCargo.ToString("P0",  ic)  + $"  (+{norm.KCargo * 100:F0}%)"),
            ("Коэффициент на город",       norm.KCity.ToString("P0",   ic)  + $"  (+{norm.KCity  * 100:F0}%)"),
            ("Коэффициент на зиму",        norm.KWinter.ToString("P0", ic)  + $"  (+{norm.KWinter* 100:F0}%)"),
        };

        for (int i = 0; i < kvPairs.Length; i++)
        {
            ws.Cell(i + 3, 1).Value = kvPairs[i].label;
            ws.Cell(i + 3, 2).Value = kvPairs[i].val;
            ws.Cell(i + 3, 1).Style.Font.Bold = true;
        }

        // ── Расчётная таблица 4×4 ─────────────────────────────────────────────
        int tableRow = 9;

        // Двойная шапка — точно как в Excel оригинале
        ws.Cell(tableRow, 1).Value = "Норма за городом";
        ws.Range(tableRow, 1, tableRow, 4).Merge();
        ws.Cell(tableRow, 5).Value = "Норма по городу";
        ws.Range(tableRow, 5, tableRow, 8).Merge();

        foreach (var col in new[] {1,5})
        {
            ws.Cell(tableRow + 1, col).Value = "Летнее";
            ws.Range(tableRow + 1, col, tableRow + 1, col + 1).Merge();
            ws.Cell(tableRow + 1, col + 2).Value = "Зимнее";
            ws.Range(tableRow + 1, col + 2, tableRow + 1, col + 3).Merge();
        }

        string[] subHdr = { "Без груза", "С грузом", "Без груза", "С грузом",
                             "Без груза", "С грузом", "Без груза", "С грузом" };
        for (int c = 0; c < 8; c++)
            ws.Cell(tableRow + 2, c + 1).Value = subHdr[c];

        // Значения
        double[] values =
        {
            norm.SummerNoCargo,     norm.SummerWithCargo,
            norm.WinterNoCargo,     norm.WinterWithCargo,
            norm.SummerNoCargoCity, norm.SummerWithCargoCity,
            norm.WinterNoCargoCity, norm.WinterWithCargoCity,
        };

        for (int c = 0; c < 8; c++)
        {
            var cell = ws.Cell(tableRow + 3, c + 1);
            cell.Value = Math.Round(values[c], 2);
            cell.Style.Font.Bold = true;
        }

        // Стили для расчётной таблицы
        var tableRange = ws.Range(tableRow, 1, tableRow + 3, 8);
        tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        tableRange.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;
        tableRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Range(tableRow, 1, tableRow, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        ws.Range(tableRow, 1, tableRow, 8).Style.Font.FontColor = XLColor.White;
        ws.Range(tableRow, 1, tableRow, 8).Style.Font.Bold = true;

        ws.Range(tableRow + 1, 1, tableRow + 2, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");
        ws.Range(tableRow + 3, 1, tableRow + 3, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF6FF");

        ws.Columns(1, 8).AdjustToContents();
        ws.Column(1).Width = Math.Max(ws.Column(1).Width, 16);
    }
}

// ─── DTO для дашборда ─────────────────────────────────────────────────────────
public class DashboardStats
{
    public int    TotalWaybills   { get; init; }
    public int    TodayCount      { get; init; }
    public int    MonthCount      { get; init; }
    public int    ActiveDrivers   { get; init; }
    public int    ActiveVehicles  { get; init; }
    public long   MonthMileage    { get; init; }
    public double MonthFuelActual { get; init; }
    public double MonthFuelNorm   { get; init; }

    /// <summary>Положительное — экономия, отрицательное — перерасход (за месяц).</summary>
    public double MonthFuelDelta => MonthFuelNorm - MonthFuelActual;
}
