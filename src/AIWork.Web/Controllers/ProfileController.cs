using AIWork.Domain;
using AIWork.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
namespace AIWork.Web.Controllers;
public class PasswordChange
{
    [Required,DataType(DataType.Password)]public string CurrentPassword{get;set;}="";
    [Required,StringLength(128,MinimumLength=12),DataType(DataType.Password)]public string NewPassword{get;set;}="";
    [Required,Compare(nameof(NewPassword)),DataType(DataType.Password)]public string ConfirmPassword{get;set;}="";
}
[Authorize]public class ProfileController(WorkDb db,IPasswordHasher<LocalUser> hasher):Controller
{
    public IActionResult Index()=>View(new PasswordChange());
    [HttpPost]public async Task<IActionResult> ChangePassword(PasswordChange input)
    {
        if(!ModelState.IsValid)return View("Index",new PasswordChange());var user=await db.Users.SingleAsync();if(user.LockedUntilUtc>DateTime.UtcNow||hasher.VerifyHashedPassword(user,user.PasswordHash,input.CurrentPassword)==PasswordVerificationResult.Failed)
        {user.FailedAttempts++;if(user.FailedAttempts>=5){user.LockedUntilUtc=DateTime.UtcNow.AddMinutes(5);user.FailedAttempts=0;}db.Audit("PasswordChangeRejected","LocalUser","1");await db.SaveChangesAsync();ModelState.AddModelError("","Mật khẩu hiện tại không đúng hoặc tài khoản tạm khóa.");return View("Index",new PasswordChange());}
        user.PasswordHash=hasher.HashPassword(user,input.NewPassword);user.SecurityStamp=Guid.NewGuid().ToString("N");user.FailedAttempts=0;user.LockedUntilUtc=null;db.Audit("PasswordChanged","LocalUser","1","Credential changed; secret omitted; sessions revoked.");await db.SaveChangesAsync();await HttpContext.SignOutAsync();return RedirectToAction("Login","Account");
    }
}
