using AIWork.Application;
using AIWork.Domain;
using AIWork.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AIWork.Web.Controllers;

[Authorize]
public class HomeController(WorkDb db) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Projects = await db.Projects.CountAsync(p => p.ArchivedAtUtc == null);
        ViewBag.InboxCount=await db.InboxItems.CountAsync(i=>i.ArchivedAtUtc==null&&i.Status=="NeedsReview");
        ViewBag.PendingActions=await db.Actions.CountAsync(a=>a.Status=="PendingApproval"||a.Status=="NeedsReview");
        ViewBag.OpenIssues=await db.TrackingEntries.CountAsync(e=>e.ArchivedAtUtc==null&&e.Kind==TrackingKind.Issue&&e.Status!=TrackingStatus.Verified&&e.Status!=TrackingStatus.Cancelled);
        ViewBag.Decisions=await db.TrackingEntries.CountAsync(e=>e.ArchivedAtUtc==null&&e.Kind==TrackingKind.Decision&&e.Status==TrackingStatus.Open);
        ViewBag.History = await db.AuditEvents.OrderByDescending(a => a.OccurredAtUtc).Take(8).ToListAsync();
        return View(await db.Tasks.Where(t => t.ArchivedAtUtc == null).ToListAsync());
    }
    [AllowAnonymous] public IActionResult Error() { Response.StatusCode = 500; return View("ErrorPage", "Có lỗi khi xử lý. Dữ liệu không bị xóa. Hãy tải lại trang và kiểm tra lịch sử trước khi thử lại."); }
    [AllowAnonymous] public IActionResult Status(int code) { Response.StatusCode = code; return View("ErrorPage", $"Mã {code}: không tìm thấy trang hoặc yêu cầu không hợp lệ."); }
}
[Authorize]
public abstract class ManagementController(WorkDb db) : Controller
{
    protected WorkDb Db => db;
    protected async Task Lists()
    {
        ViewBag.Projects = await db.Projects.OrderBy(p => p.Name).ToListAsync();
        ViewBag.Workstreams = await db.Workstreams.OrderBy(w => w.Name).ToListAsync();
    }
    protected async Task History(Guid id) => ViewBag.History = await db.AuditEvents.Where(a => a.EntityId == id.ToString()).OrderByDescending(a => a.OccurredAtUtc).ToListAsync();
    protected async Task<bool> Save()
    {
        try { await db.SaveChangesAsync(); return true; }
        catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "Bản ghi đã thay đổi ở cửa sổ khác. Tải lại trang để xem phiên bản mới trước khi sửa."); }
        catch (DbUpdateException) { ModelState.AddModelError("", "Không thể lưu: mã bị trùng hoặc liên kết dữ liệu không hợp lệ."); }
        return false;
    }
    protected void CheckVersion(Record entity, int version) => db.Entry(entity).Property(e => e.Version).OriginalValue = version;
}
public class WorkstreamsController(WorkDb db) : ManagementController(db)
{
    public async Task<IActionResult> Index(string? q, bool archived = false) => View(await Db.Workstreams.Where(w => (archived || w.ArchivedAtUtc == null) && (q == null || w.Name.Contains(q) || w.Code.Contains(q))).OrderBy(w => w.Name).ToListAsync());
    public async Task<IActionResult> Details(Guid id)
    {
        var item = await Db.Workstreams.FindAsync(id); if (item == null) return NotFound();
        ViewBag.LinkedProjects = await Db.Projects.Where(p => p.WorkstreamId == id && p.ArchivedAtUtc == null).ToListAsync();
        ViewBag.LinkedTasks = await Db.Tasks.Where(t => t.WorkstreamId == id && t.ArchivedAtUtc == null).ToListAsync();
        await History(id); return View(item);
    }
    public async Task<IActionResult> Edit(Guid? id) { await Lists(); return id == null ? View(new Workstream()) : await Db.Workstreams.FindAsync(id) is { } x ? View(x) : NotFound(); }
    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, [Bind("Id,Code,Name,Description,ParentId,Status,Version")] Workstream input)
    {
        if (id != input.Id) return BadRequest();
        var entity = await Db.Workstreams.FindAsync(id); var isNew = entity == null;
        if (input.ParentId != null)
        {
            var parent = await Db.Workstreams.FindAsync(input.ParentId); var visited = new HashSet<Guid> { id };
            if (parent == null || (parent.ArchivedAtUtc != null && entity?.ParentId != parent.Id)) ModelState.AddModelError("ParentId", "Luồng cha không hợp lệ.");
            while (parent != null) { if (!visited.Add(parent.Id)) { ModelState.AddModelError("ParentId", "Không được tạo vòng lặp luồng công việc."); break; } parent = parent.ParentId == null ? null : await Db.Workstreams.FindAsync(parent.ParentId); }
        }
        if (ModelState.IsValid)
        {
            entity ??= new Workstream { Id = input.Id }; if (isNew) Db.Workstreams.Add(entity); else CheckVersion(entity, input.Version);
            entity.Code = input.Code.Trim(); entity.Name = input.Name.Trim(); entity.Description = input.Description; entity.ParentId = input.ParentId; entity.Status = input.Status;
            if (await Save()) return RedirectToAction("Details", new { id });
        }
        await Lists(); return View(input);
    }
    [HttpPost]
    public async Task<IActionResult> Archive(Guid id, int version)
    {
        var item = await Db.Workstreams.FindAsync(id); if (item == null) return NotFound(); CheckVersion(item, version); item.ArchivedAtUtc = DateTime.UtcNow; item.Status = WorkstreamStatus.Archived;
        if (!await Save()) return Conflict("Dữ liệu đã thay đổi. Hãy tải lại trang."); return RedirectToAction("Index");
    }
}
public class ProjectsController(WorkDb db) : ManagementController(db)
{
    public async Task<IActionResult> Index(string? q, ProjectStatus? status, ProjectPhase? phase, string? owner, bool archived = false)
    {
        await Lists(); ViewBag.AllTasks = await Db.Tasks.Where(t => t.ArchivedAtUtc == null).ToListAsync();
        var query = Db.Projects.Where(p => archived || p.ArchivedAtUtc == null);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(p => p.Name.Contains(q) || p.Code.Contains(q) || (p.Objective != null && p.Objective.Contains(q)) || (p.StatusReason != null && p.StatusReason.Contains(q)));
        if (status != null) query = query.Where(p => p.Status == status); if (phase != null) query = query.Where(p => p.Phase == phase); if (!string.IsNullOrWhiteSpace(owner)) query = query.Where(p => p.OwnerText != null && p.OwnerText.Contains(owner));
        return View(await query.OrderBy(p => p.Code).ToListAsync());
    }
    public async Task<IActionResult> Details(Guid id)
    {
        var item = await Db.Projects.FindAsync(id); if (item == null) return NotFound(); await Lists(); await History(id);
        ViewBag.Tasks = await Db.Tasks.Where(t => t.ProjectId == id && t.ArchivedAtUtc == null).OrderBy(t => t.DueDate).ToListAsync(); return View(item);
    }
    public async Task<IActionResult> Edit(Guid? id) { await Lists(); return id == null ? View(new Project()) : await Db.Projects.FindAsync(id) is { } x ? View(x) : NotFound(); }
    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, [Bind("Id,Code,Name,Objective,Scope,WorkstreamId,OwnerText,Phase,Status,Health,BaselineTargetDate,CurrentTargetDate,ActualApplyDate,StatusReason,Version")] Project input)
    {
        if (id != input.Id) return BadRequest();
        if (input.WorkstreamId != null && !await Db.Workstreams.AnyAsync(w => w.Id == input.WorkstreamId && (w.ArchivedAtUtc == null || Db.Projects.Any(p => p.Id == id && p.WorkstreamId == w.Id)))) ModelState.AddModelError("WorkstreamId", "Luồng công việc không hợp lệ.");
        if (ModelState.IsValid)
        {
            var item = await Db.Projects.FindAsync(id); if (item == null) { item = new Project { Id = id }; Db.Projects.Add(item); } else CheckVersion(item, input.Version);
            item.Code = input.Code.Trim(); item.Name = input.Name.Trim(); item.Objective = input.Objective; item.Scope = input.Scope; item.WorkstreamId = input.WorkstreamId; item.OwnerText = input.OwnerText; item.Phase = input.Phase; item.Status = input.Status; item.Health = input.Health; item.BaselineTargetDate = input.BaselineTargetDate; item.CurrentTargetDate = input.CurrentTargetDate; item.ActualApplyDate = input.ActualApplyDate; item.StatusReason = input.StatusReason;
            if (await Save()) return RedirectToAction("Details", new { id });
        }
        await Lists(); return View(input);
    }
    [HttpPost]
    public async Task<IActionResult> Archive(Guid id, int version)
    {
        var item = await Db.Projects.FindAsync(id); if (item == null) return NotFound(); CheckVersion(item, version); item.ArchivedAtUtc = DateTime.UtcNow;
        if (!await Save()) return Conflict("Dữ liệu đã thay đổi. Hãy tải lại trang."); return RedirectToAction("Index");
    }
}
public class TasksController(WorkDb db) : ManagementController(db)
{
    public async Task<IActionResult> Index(string? q, Guid? project, WorkStatus? status, string? owner, string? due, string? sort, bool archived = false)
    {
        await Lists(); var query = Db.Tasks.Where(t => archived || t.ArchivedAtUtc == null);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(t => t.Title.Contains(q) || (t.Description != null && t.Description.Contains(q)) || (t.NextAction != null && t.NextAction.Contains(q)));
        if (project != null) query = query.Where(t => t.ProjectId == project); if (status != null) query = query.Where(t => t.Status == status); if (!string.IsNullOrWhiteSpace(owner)) query = query.Where(t => t.OwnerText != null && t.OwnerText.Contains(owner));
        var tasks = await query.ToListAsync(); var today = WorkRules.Today;
        tasks = due switch { "overdue" => tasks.Where(t => t.IsOverdue(today)).ToList(), "today" => tasks.Where(t => t.DueDate == today && t.Status is not (WorkStatus.Done or WorkStatus.Cancelled)).ToList(), "week" => tasks.Where(t => t.DueDate >= today && t.DueDate <= today.AddDays(7) && t.Status is not (WorkStatus.Done or WorkStatus.Cancelled)).ToList(), "review" => tasks.Where(t => t.OwnerText == null || t.OwnerText == "" || t.DueDate == null).ToList(), _ => tasks };
        return View(sort == "updated" ? tasks.OrderByDescending(t => t.UpdatedAtUtc).ToList() : sort == "priority" ? tasks.OrderByDescending(t => t.Priority).ToList() : tasks.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ToList());
    }
    public async Task<IActionResult> Details(Guid id) { var item = await Db.Tasks.FindAsync(id); if (item == null) return NotFound(); await Lists(); await History(id); return View(item); }
    public async Task<IActionResult> Edit(Guid? id, Guid? projectId) { await Lists(); return id == null ? View(new WorkTask { ProjectId = projectId }) : await Db.Tasks.FindAsync(id) is { } x ? View(x) : NotFound(); }
    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, [Bind("Id,Title,Description,ProjectId,WorkstreamId,OwnerText,Status,Priority,StartDate,DueDate,DueDateType,NextAction,CompletionEvidence,FollowUpPersonText,WaitingFor,Blocker,NextCheckAtUtc,Version")] WorkTask input)
    {
        if (id != input.Id) return BadRequest();
        if (input.ProjectId != null && !await Db.Projects.AnyAsync(p => p.Id == input.ProjectId && (p.ArchivedAtUtc == null || Db.Tasks.Any(t => t.Id == id && t.ProjectId == p.Id)))) ModelState.AddModelError("ProjectId", "Dự án không hợp lệ hoặc đã lưu trữ.");
        if (input.WorkstreamId != null && !await Db.Workstreams.AnyAsync(w => w.Id == input.WorkstreamId && (w.ArchivedAtUtc == null || Db.Tasks.Any(t => t.Id == id && t.WorkstreamId == w.Id)))) ModelState.AddModelError("WorkstreamId", "Luồng công việc không hợp lệ.");
        if (ModelState.IsValid)
        {
            var item = await Db.Tasks.FindAsync(id); if (item == null) { item = new WorkTask { Id = id }; Db.Tasks.Add(item); } else CheckVersion(item, input.Version);
            var oldStatus = item.Status; item.Title = input.Title.Trim(); item.Description = input.Description; item.ProjectId = input.ProjectId; item.WorkstreamId = input.WorkstreamId; item.OwnerText = input.OwnerText; item.Status = input.Status; item.Priority = input.Priority; item.StartDate = input.StartDate; item.DueDate = input.DueDate; item.DueDateType = input.DueDateType; item.NextAction = input.NextAction; item.CompletionEvidence = input.CompletionEvidence; item.FollowUpPersonText=input.FollowUpPersonText;item.WaitingFor=input.WaitingFor;item.Blocker=input.Blocker;item.NextCheckAtUtc=input.NextCheckAtUtc; WorkRules.Transition(item, oldStatus);
            if (await Save()) return RedirectToAction("Details", new { id });
        }
        await Lists(); return View(input);
    }
    [HttpPost]
    public async Task<IActionResult> Archive(Guid id, int version)
    {
        var item = await Db.Tasks.FindAsync(id); if (item == null) return NotFound(); CheckVersion(item, version); item.ArchivedAtUtc = DateTime.UtcNow;
        if (!await Save()) return Conflict("Dữ liệu đã thay đổi. Hãy tải lại trang."); return RedirectToAction("Index");
    }
}



