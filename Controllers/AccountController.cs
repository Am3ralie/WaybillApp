using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WaybillApp.Models;

namespace WaybillApp.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly RoleManager<IdentityRole> _roles;

    public AccountController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        RoleManager<IdentityRole> roles)
    {
        _users = users; _signIn = signIn; _roles = roles;
    }

    // GET /Account/Login
    public IActionResult Login() => View();

    // POST /Account/Login
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, bool remember)
    {
        var result = await _signIn.PasswordSignInAsync(username, password, remember, false);
        if (result.Succeeded) return RedirectToAction("Index", "Home");
        ModelState.AddModelError("", "Неверный логин или пароль");
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction("Login");
    }

    // ─── Управление пользователями (только admin) ─────────────────────────────

    [Authorize(Roles = "admin")]
    public IActionResult Users() => View(_users.Users.ToList());

    [Authorize(Roles = "admin")]
    public IActionResult CreateUser() => View();

    [Authorize(Roles = "admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(string username, string fullName,
        string password, string role)
    {
        var user = new ApplicationUser
        {
            UserName = username,
            Email = username + "@waybill.local",
            FullName = fullName,
            Role = role,
            EmailConfirmed = true
        };
        var result = await _users.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await _users.AddToRoleAsync(user, role);
            return RedirectToAction("Users");
        }
        foreach (var e in result.Errors)
            ModelState.AddModelError("", e.Description);
        return View();
    }

    [Authorize(Roles = "admin")]
    public async Task<IActionResult> EditUser(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();
        return View(user);
    }

    [Authorize(Roles = "admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(string id, string fullName, string role, string? newPassword)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();

        user.FullName = fullName;
        user.Role = role;
        await _users.UpdateAsync(user);

        // Сменить роль
        var oldRoles = await _users.GetRolesAsync(user);
        await _users.RemoveFromRolesAsync(user, oldRoles);
        await _users.AddToRoleAsync(user, role);

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            await _users.ResetPasswordAsync(user, token, newPassword);
        }

        return RedirectToAction("Users");
    }

    [Authorize(Roles = "admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user != null) await _users.DeleteAsync(user);
        return RedirectToAction("Users");
    }

    public IActionResult AccessDenied() => View();
}
