using AIWork.Domain;
using AIWork.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
namespace AIWork.Web.Controllers;
public class TrackingController(WorkDb db,TrackingService service):ManagementController(db)
{
    public async Task<IActionResult> Index(TrackingKind? kind,string? q,Guid? projectId,TrackingStatus? status)
    {
        ViewBag.Kind=kind;await Lists();var query=Db.TrackingEntries.Where(e=>e.ArchivedAtUtc==null);
        if(kind!=null)query=query.Where(e=>e.Kind==kind);if(projectId!=null)query=query.Where(e=>e.ProjectId==projectId);if(status!=null)query=query.Where(e=>e.Status==status);if(!string.IsNullOrWhiteSpace(q))query=query.Where(e=>e.Title.Contains(q)||e.Content.Contains(q));
        return View(await query.OrderByDescending(e=>e.UpdatedAtUtc).ToListAsync());
    }
    public async Task<IActionResult> Details(Guid id){var item=await Db.TrackingEntries.FindAsync(id);if(item==null)return NotFound();await Lists();await History(id);return View(item);}
    public async Task<IActionResult> Edit(Guid? id,TrackingKind kind,Guid? projectId){await Lists();ViewBag.Sources=await Db.Sources.OrderByDescending(s=>s.CreatedAtUtc).Take(100).ToListAsync();return id==null?View(new TrackingEntry{Kind=kind,ProjectId=projectId}):await Db.TrackingEntries.FindAsync(id) is {} e?View(e):NotFound();}
    [HttpPost]public async Task<IActionResult> Edit(Guid id,[Bind("Id,Kind,Title,Content,ProjectId,TaskId,SourceId,PersonText,Status,Verification,Severity,TargetDate,DueDateType,RootCause,NextAction,NextCheckAtUtc,SupersedesId,Version")]TrackingEntry input)
    {
        if(id!=input.Id)return BadRequest();
        if(ModelState.IsValid)try
        {
            await service.ValidateLinks(input.ProjectId,input.TaskId,input.SourceId);
            var item=await Db.TrackingEntries.FindAsync(id);if(item==null){item=new TrackingEntry{Id=id};Db.TrackingEntries.Add(item);}else{if(item.Kind!=input.Kind)throw new ValidationException("Không thể đổi loại hồ sơ.");CheckVersion(item,input.Version);}
            if(input.SupersedesId!=null&&(input.SupersedesId==id||!await Db.TrackingEntries.AnyAsync(e=>e.Id==input.SupersedesId&&e.Kind==TrackingKind.Decision)))throw new ValidationException("Quyết định thay thế không hợp lệ.");
            item.Kind=input.Kind;item.Title=input.Title;item.Content=input.Content;item.ProjectId=input.ProjectId;item.TaskId=input.TaskId;item.SourceId=input.SourceId;item.PersonText=input.PersonText;item.Status=input.Status;item.Verification=input.Verification;item.Severity=input.Severity;item.TargetDate=input.TargetDate;item.DueDateType=input.DueDateType;item.RootCause=input.RootCause;item.NextAction=input.NextAction;item.NextCheckAtUtc=input.NextCheckAtUtc;item.SupersedesId=input.SupersedesId;
            item.ResolvedAtUtc=item.Status is TrackingStatus.Resolved or TrackingStatus.Verified?item.ResolvedAtUtc??DateTime.UtcNow:null;
            if(await Save())return RedirectToAction("Details",new{id});
        }catch(ValidationException ex){ModelState.AddModelError("",ex.Message);}
        await Lists();ViewBag.Sources=await Db.Sources.OrderByDescending(s=>s.CreatedAtUtc).Take(100).ToListAsync();return View(input);
    }
    [HttpPost]public async Task<IActionResult> Archive(Guid id,int version){var item=await Db.TrackingEntries.FindAsync(id);if(item==null)return NotFound();CheckVersion(item,version);item.ArchivedAtUtc=DateTime.UtcNow;if(!await Save())return Conflict();return RedirectToAction("Index",new{kind=item.Kind});}
}
public class SourcesController(WorkDb db,TrackingService service):ManagementController(db)
{
    public async Task<IActionResult> Index(string? q)=>View(await Db.Sources.Where(s=>s.ArchivedAtUtc==null&&(q==null||s.Title.Contains(q)||s.OriginalText.Contains(q))).OrderByDescending(s=>s.CreatedAtUtc).Take(100).ToListAsync());
    public async Task<IActionResult> Details(Guid id){var source=await Db.Sources.FindAsync(id);if(source==null)return NotFound();ViewBag.Links=await Db.SourceLinks.Where(l=>l.SourceId==id&&l.ArchivedAtUtc==null).ToListAsync();await History(id);return View(source);}
    public IActionResult Create()=>View(new SourceRecord());
    [HttpPost]public async Task<IActionResult> Create([Bind("Kind,Title,OriginalText,OccurredAtUtc,Sender,IsConfidential")]SourceRecord input)
    {
        if(ModelState.IsValid)try{var source=service.Capture(input.Kind,input.Title,input.OriginalText,input.OccurredAtUtc,input.Sender,input.IsConfidential);await Db.SaveChangesAsync();return RedirectToAction("Details",new{id=source.Id});}catch(ValidationException ex){ModelState.AddModelError("",ex.Message);}
        return View(input);
    }
    [HttpPost]public async Task<IActionResult> UpdateMetadata(Guid id,int version,string status,bool isConfidential)
    {
        if(status is not("Captured" or "Classified" or "Linked" or "Verified" or "Actioned" or "Archived"))return BadRequest("Trạng thái nguồn không hợp lệ.");
        var source=await Db.Sources.FindAsync(id);if(source==null)return NotFound();CheckVersion(source,version);source.Status=status;source.IsConfidential=isConfidential;source.ArchivedAtUtc=status=="Archived"?DateTime.UtcNow:null;
        if(!await Save())return Conflict("Nguồn đã thay đổi; tải lại trước khi sửa.");return RedirectToAction("Details",new{id});
    }
    [HttpPost]public async Task<IActionResult> Link(Guid id,string entityType,Guid entityId)
    {try{await service.Link(id,entityType,entityId);await Db.SaveChangesAsync();}catch(ValidationException ex){TempData["Message"]=ex.Message;}return RedirectToAction("Details",new{id});}
    [HttpPost]public async Task<IActionResult> Archive(Guid id,int version){var item=await Db.Sources.FindAsync(id);if(item==null)return NotFound();CheckVersion(item,version);item.ArchivedAtUtc=DateTime.UtcNow;item.Status="Archived";if(!await Save())return Conflict();return RedirectToAction("Index");}
}
public class DependenciesController(WorkDb db,TrackingService service):ManagementController(db)
{
    public async Task<IActionResult> Index(Guid? taskId){ViewBag.Tasks=await Db.Tasks.Where(t=>t.ArchivedAtUtc==null).OrderBy(t=>t.Title).ToListAsync();return View(await Db.TaskDependencies.Where(d=>d.ArchivedAtUtc==null&&(taskId==null||d.TaskId==taskId)).ToListAsync());}
    [HttpPost]public async Task<IActionResult> Add(Guid taskId,Guid dependsOn){try{await service.AddDependency(taskId,dependsOn);await Db.SaveChangesAsync();}catch(ValidationException ex){TempData["Message"]=ex.Message;}return RedirectToAction("Index",new{taskId});}
    [HttpPost]public async Task<IActionResult> Remove(Guid id){var item=await Db.TaskDependencies.FindAsync(id);if(item==null)return NotFound();item.ArchivedAtUtc=DateTime.UtcNow;await Db.SaveChangesAsync();return RedirectToAction("Index",new{item.TaskId});}
}

