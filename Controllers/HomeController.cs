using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaybillApp.Services;

namespace WaybillApp.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly WaybillService _svc;
    public HomeController(WaybillService svc) => _svc = svc;

    public async Task<IActionResult> Index()
    {
        ViewBag.Stats = await _svc.GetStatsAsync();
        return View();
    }
}
