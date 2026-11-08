using AIWork.Application;
using AIWork.Domain;
using AIWork.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
namespace AIWork.Web.Controllers;
public class PlannerController(WorkDb db,PlannerService planner,MailService mail):ManagementController(db)
{
    public async Task<IActionResult> Index(){ViewBag.Tasks=await Db.Tasks.Where(t=>t.ArchivedAtUtc==null&&t.Status!=WorkStatus.Done&&t.Status!=WorkStatus.Cancelled).OrderBy(t=>t.DueDate).ToListAsync();ViewBag.Reminders=await Db.Reminders.OrderByDescending(r=>r.ScheduledAtUtc).Take(100).ToListAsync();ViewBag.Preferences=await Db.Preferences.SingleOrDefaultAsync()??new();ViewBag.Health=await Db.WorkerHealth.SingleOrDefaultAsync();return View(await Db.Notifications.OrderByDescending(n=>n.CreatedAtUtc).Take(100).ToListAsync());}
    public async Task<IActionResult> Details(Guid id){var reminder=await Db.Reminders.FindAsync(id);if(reminder==null)return NotFound();return RedirectToAction("Index");}
    [HttpPost]public async Task<IActionResult> Schedule(Guid taskId,DateTime scheduledLocal)
    {
        if(!ModelState.IsValid)return BadRequest();var utc=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(scheduledLocal,DateTimeKind.Unspecified),WorkRules.Vietnam);
        if(utc<=DateTime.UtcNow||utc>DateTime.UtcNow.AddDays(365)||!await Db.Tasks.AnyAsync(t=>t.Id==taskId&&t.ArchivedAtUtc==null)){TempData["Message"]="Chọn công việc và thời điểm tương lai trong 365 ngày.";return RedirectToAction("Index");}
        var key=$"manual:{taskId}:{utc.Ticks}";if(!await Db.Reminders.AnyAsync(r=>r.DedupeKey==key))Db.Reminders.Add(new Reminder{TaskId=taskId,ScheduledAtUtc=utc,DedupeKey=key});await Db.SaveChangesAsync();return RedirectToAction("Index");
    }
    [HttpPost]public async Task<IActionResult> Read(Guid id,int version,bool dismiss=false){var n=await Db.Notifications.FindAsync(id);if(n==null)return NotFound();CheckVersion(n,version);n.ReadAtUtc=DateTime.UtcNow;if(dismiss)n.DismissedAtUtc=DateTime.UtcNow;if(!await Save())return Conflict();return RedirectToAction("Index");}
    [HttpPost]public async Task<IActionResult> Snooze(Guid id,int version,DateTime untilLocal){if(!ModelState.IsValid)return BadRequest();try{await planner.Snooze(id,version,TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(untilLocal,DateTimeKind.Unspecified),WorkRules.Vietnam));}catch(Exception ex)when(ex is ValidationException or DbUpdateException){TempData["Message"]="Không hoãn được; kiểm tra thời điểm và phiên bản.";}return RedirectToAction("Index");}
    [HttpPost]public async Task<IActionResult> Cancel(Guid id,int version){var r=await Db.Reminders.FindAsync(id);if(r==null)return NotFound();CheckVersion(r,version);r.Status="Cancelled";if(!await Save())return Conflict();return RedirectToAction("Index");}
    [HttpPost]public async Task<IActionResult> SendEmail(Guid? id,string confirmRecipient,bool test,CancellationToken ct){try{var result=await mail.Send(id,confirmRecipient,test,ct);TempData["Message"]=$"Kết quả SMTP: {result.Result}. Không tự gửi lại nếu chưa rõ trạng thái.";}catch(ValidationException ex){TempData["Message"]=ex.Message;}return RedirectToAction("Index");}
}
public class MaintenanceController(WorkDb db,BackupService backups,AppPaths paths,IHostApplicationLifetime lifetime):ManagementController(db)
{
    public async Task<IActionResult> Index(){ViewBag.Schema=(await Db.Database.GetAppliedMigrationsAsync()).LastOrDefault();ViewBag.Backups=await Db.BackupManifests.OrderByDescending(b=>b.CreatedAtUtc).Take(20).ToListAsync();ViewBag.Health=await Db.WorkerHealth.SingleOrDefaultAsync();ViewBag.Deliveries=await Db.Deliveries.OrderByDescending(d=>d.CreatedAtUtc).Take(20).ToListAsync();ViewBag.DbSize=new FileInfo(Db.Database.GetDbConnection().DataSource).Length;ViewBag.Attachments=Directory.Exists(Path.Combine(paths.Data,"Attachments"))?Directory.EnumerateFiles(Path.Combine(paths.Data,"Attachments"),"*",SearchOption.AllDirectories).Sum(p=>new FileInfo(p).Length):0L;return View();}
    [HttpPost,RequestSizeLimit(100*1024*1024)]public async Task<IActionResult> Preview(IFormFile? file)
    {
        if(file==null||file.Length==0||file.Length>100*1024*1024||Path.GetExtension(file.FileName).ToLowerInvariant()!=".zip")return BadRequest("Backup ZIP tối đa 100 MB.");
        var token=Guid.NewGuid().ToString("N");var dir=Path.Combine(paths.Data,"RestoreQueue",token);Directory.CreateDirectory(dir);var package=Path.Combine(dir,"package.zip");await using(var output=System.IO.File.Create(package))await file.CopyToAsync(output);
        try{var manifest=BackupService.Validate(package,Path.Combine(dir,"preview"),Db.Database.GetMigrations().ToHashSet());TempData["RestoreToken"]=token;ViewBag.Token=token;return View(manifest);}catch(Exception ex)when(ex is ValidationException or InvalidDataException or System.Text.Json.JsonException or Microsoft.Data.Sqlite.SqliteException){TempData["Message"]="Backup bị từ chối: manifest/hash/schema/đường dẫn/database không hợp lệ. Không thay đổi dữ liệu đang dùng.";return RedirectToAction("Index");}
    }
    [HttpPost]public async Task<IActionResult> Confirm(string token,string confirmation)
    {
        if(confirmation!="PHUC HOI"||TempData["RestoreToken"] as string!=token)return BadRequest("Cần bản xem trước mới và xác nhận PHUC HOI.");
        await backups.QueueRestore(token,Db.Actor);Response.OnCompleted(()=>{lifetime.StopApplication();return Task.CompletedTask;});return Content("Đã lưu backup trước phục hồi và xếp gói đã xác minh. Ứng dụng đang dừng sạch. Chạy lại Start.bat để phục hồi offline. Database/attachments trước phục hồi được giữ trong Data/Backups/pre-restore-files-*. Sau phục hồi đăng nhập lại; AI/SMTP bị tắt để rà soát quyền dữ liệu.","text/plain; charset=utf-8");
    }
}
