using AIWork.Domain;
using AIWork.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace AIWork.Web.Controllers;
public class AppearanceController(WorkDb db):ManagementController(db)
{
    public async Task<IActionResult> Index()=>View(await Db.Experience.SingleOrDefaultAsync()??new ExperienceSettings());
    public async Task<IActionResult> Current()=>Json(await Db.Experience.AsNoTracking().SingleOrDefaultAsync()??new ExperienceSettings());
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]public async Task<IActionResult> Styles()
    {
        var p=await Db.Experience.AsNoTracking().SingleOrDefaultAsync()??new();string Safe(string c)=>System.Text.RegularExpressions.Regex.IsMatch(c??"","^#[0-9a-fA-F]{6}$")?c!:"#3665e8";
        var css=$":root{{--accent-home:{Safe(p.HomeAccent)};--accent-ai:{Safe(p.UnifiedAccent?p.HomeAccent:p.AIAccent)};--accent-work:{Safe(p.UnifiedAccent?p.HomeAccent:p.WorkAccent)};--accent-planning:{Safe(p.UnifiedAccent?p.HomeAccent:p.PlanningAccent)};--accent-settings:{Safe(p.UnifiedAccent?p.HomeAccent:p.SettingsAccent)};}}";
        try{using var json=JsonDocument.Parse(p.PageAccentsJson);foreach(var entry in json.RootElement.EnumerateObject())if(System.Text.RegularExpressions.Regex.IsMatch(entry.Name,"^[A-Za-z]{1,40}$")&&entry.Value.ValueKind==JsonValueKind.String)css+=$"body[data-controller='{entry.Name}']{{--accent:{Safe(entry.Value.GetString()!)};}}";}catch(Exception ex)when(ex is JsonException or InvalidOperationException){}
        return Content(css,"text/css");
    }
    [HttpPost]public async Task<IActionResult> Save(ExperienceSettings input)
    {
        if(!ModelState.IsValid)return View("Index",input);
        if(input.PageAccentsJson.Length>4000)ModelState.AddModelError("PageAccentsJson","Tối đa 4000 ký tự.");
        try{using var json=JsonDocument.Parse(input.PageAccentsJson);foreach(var p in json.RootElement.EnumerateObject())if(!System.Text.RegularExpressions.Regex.IsMatch(p.Name,"^[A-Za-z]{1,40}$")||p.Value.ValueKind!=JsonValueKind.String||!System.Text.RegularExpressions.Regex.IsMatch(p.Value.GetString()!,"^#[0-9a-fA-F]{6}$"))ModelState.AddModelError("PageAccentsJson","Tên trang hoặc màu không hợp lệ.");}catch(Exception ex)when(ex is JsonException or InvalidOperationException){ModelState.AddModelError("PageAccentsJson","JSON màu trang không hợp lệ.");}
        if(!ModelState.IsValid)return View("Index",input);var current=await Db.Experience.SingleOrDefaultAsync();if(current==null){current=new();Db.Experience.Add(current);}var before=JsonSerializer.Serialize(current);Db.Entry(current).CurrentValues.SetValues(input);current.Id=1;Db.Audit("AppearanceUpdated","ExperienceSettings","1",JsonSerializer.Serialize(new{before,after=input}));await Db.SaveChangesAsync();return RedirectToAction("Index");
    }
}
public class DashboardController(WorkDb db,DashboardService dashboard,AppPaths paths,IConfiguration config):ManagementController(db)
{
    public async Task<IActionResult> Data(Guid? workstreamId,ProjectStatus? status,DateOnly? from,DateOnly? until){if(!ModelState.IsValid||from>until)return BadRequest("Bộ lọc ngày không hợp lệ.");return Json(await dashboard.Snapshot(workstreamId,status,from,until));}
    [HttpPost]public async Task<IActionResult> UpdateTask([FromBody]GridTaskEdit input)
    {
        if(!ModelState.IsValid)return BadRequest("Dữ liệu không hợp lệ.");var item=await Db.Tasks.FindAsync(input.Id);if(item==null||item.ArchivedAtUtc!=null)return NotFound();if(item.Version!=input.Version)return Conflict("Bản ghi đã thay đổi; tải lại dữ liệu.");
        if(!Enum.TryParse<WorkStatus>(input.Status,out var state)||!Enum.IsDefined(state)||!Enum.TryParse<Priority>(input.Priority,out var priority)||!Enum.IsDefined(priority)||input.OwnerText?.Length>200||input.NextAction?.Length>4000)return BadRequest("Trạng thái/ưu tiên/nội dung không hợp lệ.");
        DeadlineType? type=null;if(input.DueDate!=null){if(!Enum.TryParse<DeadlineType>(input.DueDateType,out var parsed)||!Enum.IsDefined(parsed))return BadRequest("Cần loại hạn rõ ràng.");type=parsed;}else if(!string.IsNullOrEmpty(input.DueDateType))return BadRequest("Ngày và loại hạn phải cùng trống.");
        if(item.StartDate>input.DueDate)return BadRequest("Hạn phải sau ngày bắt đầu.");CheckVersion(item,input.Version);var old=item.Status;item.Status=state;item.Priority=priority;item.OwnerText=input.OwnerText;item.NextAction=input.NextAction;item.DueDate=input.DueDate;item.DueDateType=type;AIWork.Application.WorkRules.Transition(item,old);
        if(!await Save())return Conflict("Không lưu được do xung đột.");return Json(new{status="Executed",item.Id,item.Version});
    }
    public IActionResult Components()
    {
        var folder=Path.Combine(paths.Data,"LicensedUI");var names=new[]{"dx.all.js","dx.light.css","dx.dark.css","devextreme-license.js"};var ready=config.GetValue<bool>("DevExtreme:Enabled")&&names.All(n=>System.IO.File.Exists(Path.Combine(folder,n)));
        return Json(new{devExtremeReady=ready,scripts=ready?new[]{"/Dashboard/Asset?name=devextreme-license.js","/Dashboard/Asset?name=dx.all.js"}:Array.Empty<string>(),reason=ready?"Licensed assets supplied locally":"Licensed DevExtreme assets not configured; standard dashboard active"});
    }
    public IActionResult Asset(string name){if(name is not("dx.all.js" or "dx.light.css" or "dx.dark.css" or "devextreme-license.js")||!config.GetValue<bool>("DevExtreme:Enabled"))return NotFound();var file=Path.Combine(paths.Data,"LicensedUI",name);return System.IO.File.Exists(file)?PhysicalFile(file,name.EndsWith(".css")?"text/css":"application/javascript"):NotFound();}
}
public class GridTaskEdit
{
    public Guid Id{get;set;}public int Version{get;set;}public string Status{get;set;}="Todo";public string Priority{get;set;}="Normal";public string? OwnerText{get;set;}public DateOnly? DueDate{get;set;}public string? DueDateType{get;set;}public string? NextAction{get;set;}
}
