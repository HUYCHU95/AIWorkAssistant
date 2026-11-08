using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AIWork.Domain;
using AIWork.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AIWork.Web.Controllers;

public class Credentials
{
    [Required, StringLength(64, MinimumLength = 3), RegularExpression(@"[a-zA-Z0-9._-]+", ErrorMessage = "Tên đăng nhập chỉ gồm chữ Latin, số, dấu chấm, gạch dưới hoặc gạch ngang.")] public string UserName { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 12), DataType(DataType.Password)] public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
}
public class AccountController(WorkDb db, IPasswordHasher<LocalUser> hasher) : Controller
{
    [HttpGet] public async Task<IActionResult> Setup() => await db.Users.AnyAsync() ? RedirectToAction("Login") : View(new Credentials());
    [HttpPost]
    public async Task<IActionResult> Setup(Credentials input)
    {
        if (await db.Users.AnyAsync()) return RedirectToAction("Login");
        if (!ModelState.IsValid) return View(input);
        var user = new LocalUser { UserName = input.UserName.Trim() }; user.PasswordHash = hasher.HashPassword(user, input.Password);
        db.Users.Add(user); db.Actor = user.UserName; db.Audit("Setup", "LocalUser", "1");
        try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return RedirectToAction("Login"); }
        await SignIn(user); return RedirectToAction("Index", "Home");
    }
    [HttpGet] public async Task<IActionResult> Login() => !await db.Users.AnyAsync() ? RedirectToAction("Setup") : View(new Credentials());
    [HttpPost]
    public async Task<IActionResult> Login(Credentials input)
    {
        if (!ModelState.IsValid) return View(input);
        var user = await db.Users.SingleOrDefaultAsync();
        if (user == null) return RedirectToAction("Setup");
        if (user.LockedUntilUtc > DateTime.UtcNow) { ModelState.AddModelError("", "Tạm khóa đăng nhập. Hãy thử lại sau 5 phút."); return View(input); }
        var valid = user.UserName == input.UserName && hasher.VerifyHashedPassword(user, user.PasswordHash, input.Password) != PasswordVerificationResult.Failed;
        db.Actor = valid ? user.UserName : "Anonymous";
        if (!valid)
        {
            user.FailedAttempts++; if (user.FailedAttempts >= 5) { user.LockedUntilUtc = DateTime.UtcNow.AddMinutes(5); user.FailedAttempts = 0; }
            db.Audit("LoginFailed", "LocalUser", "1"); await db.SaveChangesAsync();
            ModelState.AddModelError("", "Thông tin đăng nhập không đúng."); return View(input);
        }
        user.FailedAttempts = 0; user.LockedUntilUtc = null; db.Audit("Login", "LocalUser", "1", input.RememberMe ? "Remembered login: 30 days; password omitted." : "Session login: 8 hours; password omitted."); await db.SaveChangesAsync();
        await SignIn(user, input.RememberMe); return RedirectToAction("Index", "Home");
    }
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        if (User.Identity?.IsAuthenticated == true) { db.Audit("Logout", "LocalUser", "1"); await db.SaveChangesAsync(); }
        await HttpContext.SignOutAsync(); return RedirectToAction("Login");
    }
    private Task SignIn(LocalUser user, bool rememberMe = false) => HttpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, user.UserName), new Claim(ClaimTypes.NameIdentifier, "1"), new Claim("security_stamp", user.SecurityStamp)], CookieAuthenticationDefaults.AuthenticationScheme)),
        new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8),
            AllowRefresh = true
        });
}

