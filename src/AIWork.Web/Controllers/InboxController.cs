using AIWork.Domain;
using AIWork.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
namespace AIWork.Web.Controllers;
public class CaptureInput
{
    [Required,StringLength(300)]public string Title{get;set;}="";
    [Required,StringLength(60000)]public string Body{get;set;}="";
    [EnumDataType(typeof(SourceKind))]public SourceKind Kind{get;set;}
    [StringLength(200)]public string? Sender{get;set;}
    public DateTime? OccurredAtUtc{get;set;}
    public bool IsConfidential{get;set;}=true;
    public Guid? ProjectId{get;set;}
    public string CaptureKey{get;set;}=Guid.NewGuid().ToString();
    public List<IFormFile> Files{get;set;}=[];
}
public class InboxController(WorkDb db,InboxService inbox,AttachmentService attachments):ManagementController(db)
{
    public async Task<IActionResult> Index(string? q,string? status){ViewBag.Sources=await Db.Sources.OrderByDescending(s=>s.CreatedAtUtc).ToListAsync();return View(await Db.InboxItems.Where(i=>i.ArchivedAtUtc==null&&(status==null||i.Status==status)&&(q==null||Db.Sources.Any(s=>s.Id==i.SourceId&&(s.Title.Contains(q)||s.OriginalText.Contains(q))))).OrderByDescending(i=>i.CreatedAtUtc).Take(100).ToListAsync());}
    public async Task<IActionResult> Capture(){await Lists();return View(new CaptureInput());}
    [HttpPost,RequestSizeLimit(30*1024*1024)]public async Task<IActionResult> Capture(CaptureInput input)
    {
        if(input.Files.Count>8||input.Files.Sum(f=>f.Length)>25*1024*1024)ModelState.AddModelError("Files","Tối đa 8 file, tổng 25 MB.");
        if(ModelState.IsValid)try
        {
            var old=await Db.InboxItems.SingleOrDefaultAsync(i=>i.CaptureKey==input.CaptureKey);if(old!=null)return RedirectToAction("Details",new{id=old.Id});
            var files=new List<CheckedAttachment>();foreach(var file in input.Files){if(file.Length>5*1024*1024)throw new ValidationException("Mỗi tệp tối đa 5 MB.");using var bytes=new MemoryStream();await file.CopyToAsync(bytes);files.Add(AttachmentService.Check(file.FileName,bytes.ToArray()));}
            await using var tx=await Db.Database.BeginTransactionAsync();var item=await inbox.Capture(input.Kind,input.Title,input.Body,input.OccurredAtUtc,input.Sender,input.IsConfidential,input.ProjectId,input.CaptureKey);
            if(Guid.TryParse(Request.Form["analysisSourceId"],out var analysisSource)){await new TrackingService(Db).Link(analysisSource,"Source",item.SourceId);}
            var saved=new List<DocumentRecord>();try{foreach(var f in files)saved.Add(await attachments.Store(item.SourceId,f));await Db.SaveChangesAsync();await tx.CommitAsync();}catch{foreach(var d in saved)if(System.IO.File.Exists(attachments.PathFor(d)))System.IO.File.Delete(attachments.PathFor(d));throw;}
            return RedirectToAction("Details",new{id=item.Id});
        }catch(ValidationException ex){ModelState.AddModelError("",ex.Message);}
        await Lists();return View(input);
    }
    public async Task<IActionResult> Details(Guid id){var item=await Db.InboxItems.FindAsync(id);if(item==null)return NotFound();ViewBag.Source=await Db.Sources.FindAsync(item.SourceId);ViewBag.Documents=await Db.Documents.Where(d=>d.SourceId==item.SourceId||Db.SourceLinks.Any(l=>l.EntityType=="Source"&&l.EntityId==item.SourceId&&l.SourceId==d.SourceId)).ToListAsync();ViewBag.Actions=await Db.Actions.Where(a=>a.OriginSourceId==item.SourceId).ToListAsync();return View(item);}
    [HttpPost]public async Task<IActionResult> Extract(Guid id){try{var n=await inbox.ExtractChecklist(id);TempData["Message"]=$"Đã tạo {n} đề xuất rà soát theo checklist. Không tạo công việc trước xác nhận.";}catch(ValidationException ex){TempData["Message"]=ex.Message;}return RedirectToAction("Details",new{id});}
    [HttpPost]public async Task<IActionResult> Complete(Guid id,int version){var item=await Db.InboxItems.FindAsync(id);if(item==null)return NotFound();CheckVersion(item,version);if(await Db.Actions.AnyAsync(a=>a.OriginSourceId==item.SourceId&&(a.Status=="PendingApproval"||a.Status=="NeedsReview"))){TempData["Message"]="Vẫn còn đề xuất chờ rà soát.";return RedirectToAction("Details",new{id});}item.Status="Processed";item.ReviewedAtUtc=DateTime.UtcNow;if(!await Save())return Conflict();return RedirectToAction("Details",new{id});}
}
public class DocumentsController(WorkDb db,AttachmentService attachments):ManagementController(db)
{
    public async Task<IActionResult> Download(Guid id){var d=await Db.Documents.FindAsync(id);if(d==null||d.ArchivedAtUtc!=null)return NotFound();var path=attachments.PathFor(d);if(!System.IO.File.Exists(path))return NotFound();var bytes=await System.IO.File.ReadAllBytesAsync(path);if(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))!=d.Sha256)return Conflict("Checksum tệp không khớp.");return File(bytes,d.ContentType,d.OriginalFileName);}
}
public class MemoryController(WorkDb db,MemoryService memory):ManagementController(db)
{
    public async Task<IActionResult> Index(string? q,bool conflicts=false){ViewBag.Conflicts=conflicts;return View(await Db.MemoryFacts.Where(f=>(!conflicts||f.Verification==Verification.Conflicted)&&(q==null||f.ValueText.Contains(q)||f.Predicate.Contains(q))).OrderByDescending(f=>f.CreatedAtUtc).Take(100).ToListAsync());}
    public async Task<IActionResult> Create(Guid? sourceId){ViewBag.Sources=await Db.Sources.OrderByDescending(s=>s.CreatedAtUtc).Take(100).ToListAsync();return View(new MemoryFact{SourceId=sourceId??Guid.Empty});}
    [HttpPost]public async Task<IActionResult> Create([Bind("SubjectType,SubjectId,Predicate,ValueText,SourceId,EffectiveAtUtc")]MemoryFact input)
    {if(ModelState.IsValid)try{var f=await memory.Add(input);return RedirectToAction("Details",new{id=f.Id});}catch(ValidationException ex){ModelState.AddModelError("",ex.Message);}ViewBag.Sources=await Db.Sources.OrderByDescending(s=>s.CreatedAtUtc).Take(100).ToListAsync();return View(input);}
    public async Task<IActionResult> Details(Guid id){var f=await Db.MemoryFacts.FindAsync(id);if(f==null)return NotFound();ViewBag.Revisions=await Db.MemoryRevisions.Where(r=>r.FactId==id).OrderByDescending(r=>r.CreatedAtUtc).ToListAsync();await History(id);return View(f);}
    [HttpPost]public async Task<IActionResult> Verify(Guid id,int version,string reason){try{await memory.Verify(id,version,reason);TempData["Message"]="Đã xác minh dữ kiện; phiên bản trước giữ trong lịch sử. Không tự thay đổi mốc cam kết dự án.";}catch(Exception ex)when(ex is ValidationException or DbUpdateException){TempData["Message"]="Không thể xác minh: cần lý do hợp lệ và phiên bản mới nhất.";}return RedirectToAction("Details",new{id});}
}
