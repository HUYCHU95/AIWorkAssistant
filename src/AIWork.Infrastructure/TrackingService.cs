using AIWork.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
namespace AIWork.Infrastructure;
public class TrackingService(WorkDb db)
{
    public static void Validate(object entity){var errors=new List<ValidationResult>();if(!Validator.TryValidateObject(entity,new ValidationContext(entity),errors,true))throw new ValidationException(string.Join(" ",errors.Select(e=>e.ErrorMessage)));}
    public async Task ValidateLinks(Guid? projectId,Guid? taskId,Guid? sourceId)
    {
        if(projectId!=null&&!await db.Projects.AnyAsync(p=>p.Id==projectId))throw new ValidationException("Dự án không tồn tại.");
        if(taskId!=null&&!await db.Tasks.AnyAsync(t=>t.Id==taskId))throw new ValidationException("Công việc không tồn tại.");
        if(sourceId!=null&&!await db.Sources.AnyAsync(s=>s.Id==sourceId))throw new ValidationException("Nguồn không tồn tại.");
        if(taskId!=null&&projectId!=null&&!await db.Tasks.AnyAsync(t=>t.Id==taskId&&t.ProjectId==projectId))throw new ValidationException("Công việc thuộc dự án khác; cần rà soát liên kết.");
    }
    public SourceRecord Capture(SourceKind kind,string title,string body,DateTime? occurred,string? sender,bool confidential)
    {
        var s=new SourceRecord{Kind=kind,Title=title.Trim(),OriginalText=body,OccurredAtUtc=occurred,Sender=sender,IsConfidential=confidential,CapturedBy=db.Actor,OriginalSha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)))};
        Validate(s);db.Sources.Add(s);return s;
    }
    public async Task Link(Guid sourceId,string entityType,Guid entityId)
    {
        if(!await db.Sources.AnyAsync(s=>s.Id==sourceId))throw new ValidationException("Nguồn không tồn tại.");
        var exists=entityType switch{"Workstream"=>await db.Workstreams.AnyAsync(w=>w.Id==entityId),"Source"=>await db.Sources.AnyAsync(s=>s.Id==entityId),"Project"=>await db.Projects.AnyAsync(p=>p.Id==entityId),"Task"=>await db.Tasks.AnyAsync(t=>t.Id==entityId),"TrackingEntry"=>await db.TrackingEntries.AnyAsync(t=>t.Id==entityId),_=>false};
        if(!exists)throw new ValidationException("Đích liên kết không hợp lệ.");
        if(!await db.SourceLinks.AnyAsync(l=>l.SourceId==sourceId&&l.EntityType==entityType&&l.EntityId==entityId))db.SourceLinks.Add(new SourceLink{SourceId=sourceId,EntityType=entityType,EntityId=entityId});
    }
    public async Task AddDependency(Guid taskId,Guid dependsOn)
    {
        if(taskId==dependsOn||!await db.Tasks.AnyAsync(t=>t.Id==taskId&&t.ArchivedAtUtc==null)||!await db.Tasks.AnyAsync(t=>t.Id==dependsOn&&t.ArchivedAtUtc==null))throw new ValidationException("Liên kết phụ thuộc không hợp lệ.");
        var edges=await db.TaskDependencies.Where(d=>d.ArchivedAtUtc==null).ToListAsync();var pending=new Stack<Guid>();pending.Push(dependsOn);var seen=new HashSet<Guid>();
        while(pending.TryPop(out var node)){if(node==taskId)throw new ValidationException("Phụ thuộc sẽ tạo vòng lặp.");if(!seen.Add(node))continue;foreach(var edge in edges.Where(d=>d.TaskId==node))pending.Push(edge.DependsOnTaskId);}
        var old=await db.TaskDependencies.SingleOrDefaultAsync(d=>d.TaskId==taskId&&d.DependsOnTaskId==dependsOn);
        if(old==null)db.TaskDependencies.Add(new TaskDependency{TaskId=taskId,DependsOnTaskId=dependsOn});else old.ArchivedAtUtc=null;
    }
}
