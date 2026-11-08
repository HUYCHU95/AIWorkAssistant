using AIWork.Application;
using AIWork.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace AIWork.Infrastructure;
public class ActionArguments
{
    public string? Code{get;set;}
    public string? Title{get;set;}public string? Body{get;set;}public string? OwnerText{get;set;}public string? NextAction{get;set;}
    public Guid? ProjectId{get;set;}public Guid? WorkstreamId{get;set;}public Guid? TaskId{get;set;}public Guid? SourceId{get;set;}public Guid? EntityId{get;set;}
    public int? ExpectedVersion{get;set;}public WorkStatus? Status{get;set;}public Priority? Priority{get;set;}
    public DateOnly? DueDate{get;set;}public DeadlineType? DueDateType{get;set;}public DateTime? AtUtc{get;set;}
    public DateOnly? ProjectTarget{get;set;}public bool ClearDueDate{get;set;}
    public string? IdempotencyKey{get;set;}
}
public class ActionService(WorkDb db,TrackingService tracking)
{
    public static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,Converters={new JsonStringEnumConverter()}};
    public static readonly string[] Names=["create_project","create_workstream","create_decision","capture_note","create_task","update_task","add_project_update","create_issue","create_commitment","register_instruction","schedule_reminder","link_source","propose_change"];
    public async Task<ActionReceipt> Propose(string name,string json,Conversation context,Guid userMessageId)
    {
        if(!Names.Contains(name))throw new ValidationException("Tool không được phép.");
        var message=await db.Messages.SingleOrDefaultAsync(m=>m.Id==userMessageId&&m.ConversationId==context.Id&&m.Role=="user")??throw new ValidationException("Thiếu câu lệnh người dùng đã đăng nhập.");
        if(json.Length>18000)throw new ValidationException("Đối số quá dài.");
        var args=JsonSerializer.Deserialize<ActionArguments>(json,Json)??throw new ValidationException("Đối số không hợp lệ.");args.IdempotencyKey=null;
        var canonical=JsonSerializer.Serialize(args,Json);var key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userMessageId+":"+name+":"+canonical)));
        var old=await db.Actions.SingleOrDefaultAsync(a=>a.IdempotencyKey==key);if(old!=null)return old;
        var receipt=new ActionReceipt{ConversationId=context.Id,UserMessageId=userMessageId,OriginSourceId=args.SourceId,ToolName=name,ArgumentsJson=canonical,IdempotencyKey=key,Summary="Chưa thực hiện. Cần người dùng xem đề xuất và xác nhận.",RiskLevel=name is "update_task" or "propose_change"?"A3":"A2"};
        try
        {
            await Validate(args,name,context);
            // Imported instructions and model text never carry approval. All model writes are reviewed locally.
        }catch(ValidationException ex){receipt.Status="NeedsReview";receipt.Summary=ex.Message;receipt.OriginSourceId=null;}
        db.Actions.Add(receipt);await db.SaveChangesAsync();return receipt;
    }
    private async Task Validate(ActionArguments a,string name,Conversation? scope)
    {
        if(name=="create_task"&&await db.Tasks.AnyAsync(t=>t.ArchivedAtUtc==null&&t.Title==a.Title&&t.ProjectId==a.ProjectId&&t.WorkstreamId==a.WorkstreamId))throw new ValidationException("Công việc cùng tên và ngữ cảnh đã tồn tại; hãy liên kết nguồn thay vì tạo trùng.");
        if(name is "create_project" or "create_workstream")
        {
            if(string.IsNullOrWhiteSpace(a.Code)||a.Code.Length>40||string.IsNullOrWhiteSpace(a.Title)||a.Title.Length>(name=="create_project"?300:200)||a.Body?.Length>4000)throw new ValidationException("Cần mã, tên và mô tả đúng giới hạn.");
            if(name=="create_project"?await db.Projects.AnyAsync(p=>p.Code==a.Code):await db.Workstreams.AnyAsync(w=>w.Code==a.Code))throw new ValidationException("Mã đã tồn tại; hãy rà soát trùng lặp.");
        }
        if(name=="create_decision"&&(string.IsNullOrWhiteSpace(a.Title)||string.IsNullOrWhiteSpace(a.Body)))throw new ValidationException("Quyết định cần tiêu đề và nội dung nguồn.");
        await tracking.ValidateLinks(a.ProjectId,a.TaskId,a.SourceId);
        if(scope?.ProjectId!=null&&a.ProjectId!=scope.ProjectId)throw new ValidationException("Đề xuất ngoài dự án đang chọn; cần rà soát.");
        if(scope?.TaskId!=null&&a.TaskId!=scope.TaskId&&a.EntityId!=scope.TaskId)throw new ValidationException("Đề xuất ngoài công việc đang chọn.");
        if(a.WorkstreamId!=null&&!await db.Workstreams.AnyAsync(w=>w.Id==a.WorkstreamId&&w.ArchivedAtUtc==null))throw new ValidationException("Luồng công việc không hợp lệ.");
        if(a.ProjectId!=null&&a.WorkstreamId!=null)throw new ValidationException("Không được xung đột dự án/luồng.");
        if(a.DueDate.HasValue!=a.DueDateType.HasValue)throw new ValidationException("Ngày và loại hạn chưa rõ; không tự suy đoán.");
        if(a.Status!=null&&!Enum.IsDefined(a.Status.Value)||a.Priority!=null&&!Enum.IsDefined(a.Priority.Value)||a.DueDateType!=null&&!Enum.IsDefined(a.DueDateType.Value))throw new ValidationException("Trạng thái/ưu tiên/loại hạn không hợp lệ.");
        if(a.Title?.Length>500||a.Body?.Length>8000||a.OwnerText?.Length>200||a.NextAction?.Length>4000)throw new ValidationException("Nội dung vượt giới hạn.");
        if(name is "create_task" or "create_issue" or "create_commitment" or "register_instruction" or "capture_note" && string.IsNullOrWhiteSpace(a.Title))throw new ValidationException("Thiếu tiêu đề.");
        if(name is "create_commitment" or "register_instruction" && string.IsNullOrWhiteSpace(a.OwnerText))throw new ValidationException("Chưa rõ người cam kết/ra chỉ thị. Giữ NeedsReview, không tự gán người.");
        if(name is "capture_note" or "create_issue" or "create_commitment" or "register_instruction" or "add_project_update" && string.IsNullOrWhiteSpace(a.Body))throw new ValidationException("Thiếu nội dung nguyên văn.");
        if(name is "update_task")
        {
            var task=await db.Tasks.SingleOrDefaultAsync(t=>t.Id==a.EntityId&&t.ArchivedAtUtc==null)??throw new ValidationException("Công việc không tồn tại.");
            if(a.ExpectedVersion!=task.Version)throw new ValidationException("Version đã thay đổi; đề xuất hết hiệu lực.");
            if(scope?.ProjectId!=null&&task.ProjectId!=scope.ProjectId)throw new ValidationException("Công việc ngoài dự án đang chọn.");
        }
        if(name=="propose_change")
        {
            var p=await db.Projects.SingleOrDefaultAsync(p=>p.Id==a.EntityId&&p.ArchivedAtUtc==null)??throw new ValidationException("Dự án không tồn tại.");
            if(a.ExpectedVersion!=p.Version||a.ProjectTarget==null)throw new ValidationException("Cần version hiện tại và mốc mục tiêu cụ thể.");
            if(scope?.ProjectId!=null&&p.Id!=scope.ProjectId)throw new ValidationException("Dự án ngoài ngữ cảnh.");
        }
        if(name=="schedule_reminder"&&(a.AtUtc==null||a.AtUtc<DateTime.UtcNow.AddMinutes(-1)||a.TaskId==null))throw new ValidationException("Cần công việc và thời điểm UTC tương lai rõ ràng.");
        if(name=="link_source"&&(a.SourceId==null||(a.ProjectId==null&&a.TaskId==null)))throw new ValidationException("Cần nguồn và thực thể đích.");
        if(name=="add_project_update"&&a.ProjectId==null)throw new ValidationException("Thiếu dự án cần cập nhật.");
    }
    public async Task<ActionReceipt> Approve(Guid id,int version)
    {
        await using var tx=await db.Database.BeginTransactionAsync();var receipt=await db.Actions.FindAsync(id)??throw new ValidationException("Không tìm thấy đề xuất.");
        if(receipt.Status=="Executed")return receipt;
        if(receipt.Status!="PendingApproval"||receipt.Version!=version)throw new ValidationException("Đề xuất không còn chờ hoặc đã thay đổi.");
        db.Entry(receipt).Property(r=>r.Version).OriginalValue=version;
        var a=JsonSerializer.Deserialize<ActionArguments>(receipt.ArgumentsJson,Json)!;var scope=receipt.ConversationId!=null?await db.Conversations.FindAsync(receipt.ConversationId):null;await Validate(a,receipt.ToolName,scope);
        Record? entity=null;
        switch(receipt.ToolName)
        {
            case "create_project":
                var createdProject=new Project{Code=a.Code!,Name=a.Title!,Objective=a.Body,WorkstreamId=a.WorkstreamId,OwnerText=a.OwnerText,Health=Health.Unknown};TrackingService.Validate(createdProject);db.Projects.Add(createdProject);entity=createdProject;receipt.EntityType="Project";break;
            case "create_workstream":
                var stream=new Workstream{Code=a.Code!,Name=a.Title!,Description=a.Body};TrackingService.Validate(stream);db.Workstreams.Add(stream);entity=stream;receipt.EntityType="Workstream";break;
            case "create_task":
                var task=new WorkTask{Title=a.Title!,Description=a.Body,ProjectId=a.ProjectId,WorkstreamId=a.WorkstreamId,OwnerText=a.OwnerText,DueDate=a.DueDate,DueDateType=a.DueDateType,Priority=a.Priority??Priority.Normal,SourceLabel=a.SourceId!=null?"Source:"+a.SourceId:"Confirmed chat action"};TrackingService.Validate(task);db.Tasks.Add(task);entity=task;receipt.EntityType="Task";break;
            case "update_task":
                var current=await db.Tasks.FindAsync(a.EntityId)??throw new ValidationException("Không tìm thấy công việc.");receipt.BeforeJson=JsonSerializer.Serialize(current);db.Entry(current).Property(t=>t.Version).OriginalValue=a.ExpectedVersion!.Value;
                if(a.Title!=null)current.Title=a.Title;if(a.Body!=null)current.Description=a.Body;if(a.OwnerText!=null)current.OwnerText=a.OwnerText;if(a.NextAction!=null)current.NextAction=a.NextAction;if(a.Priority!=null)current.Priority=a.Priority.Value;
                if(a.ClearDueDate){current.DueDate=null;current.DueDateType=null;}else if(a.DueDate!=null){current.DueDate=a.DueDate;current.DueDateType=a.DueDateType;}
                if(a.Status!=null){var old=current.Status;current.Status=a.Status.Value;WorkRules.Transition(current,old);}TrackingService.Validate(current);entity=current;receipt.EntityType="Task";break;
            case "propose_change":
                var project=await db.Projects.FindAsync(a.EntityId)??throw new ValidationException("Không tìm thấy dự án.");receipt.BeforeJson=JsonSerializer.Serialize(project);db.Entry(project).Property(p=>p.Version).OriginalValue=a.ExpectedVersion!.Value;project.CurrentTargetDate=a.ProjectTarget;entity=project;receipt.EntityType="Project";break;
            case "capture_note":case "add_project_update":
                var source=tracking.Capture(SourceKind.Note,a.Title??"Cập nhật dự án",a.Body!,a.AtUtc,null,true);entity=source;receipt.EntityType="Source";if(a.ProjectId!=null)db.SourceLinks.Add(new SourceLink{SourceId=source.Id,EntityType="Project",EntityId=a.ProjectId.Value});break;
            case "create_decision":case "create_issue":case "create_commitment":case "register_instruction":
                var entry=new TrackingEntry{Kind=receipt.ToolName=="create_issue"?TrackingKind.Issue:receipt.ToolName=="create_commitment"?TrackingKind.Commitment:TrackingKind.Instruction,Title=a.Title!,Content=a.Body!,ProjectId=a.ProjectId,TaskId=a.TaskId,SourceId=a.SourceId,PersonText=a.OwnerText,TargetDate=a.DueDate,DueDateType=a.DueDateType,Severity=a.Priority??Priority.Normal,Status=receipt.ToolName=="create_commitment"?TrackingStatus.Waiting:TrackingStatus.Open};TrackingService.Validate(entry);db.TrackingEntries.Add(entry);entity=entry;receipt.EntityType="TrackingEntry";break;
            case "link_source":await tracking.Link(a.SourceId!.Value,a.TaskId!=null?"Task":"Project",(a.TaskId??a.ProjectId)!.Value);receipt.EntityType="Source";receipt.EntityId=a.SourceId;break;
            case "schedule_reminder":await Schedule(a,receipt);break;
            default:throw new ValidationException("Không hỗ trợ thực thi tool này.");
        }
        if(entity is TrackingEntry decision&&receipt.ToolName=="create_decision")decision.Kind=TrackingKind.Decision;
        if(entity!=null){receipt.EntityId=entity.Id;receipt.AppliedVersion=entity.Version+1;receipt.AfterJson=JsonSerializer.Serialize(entity,entity.GetType());if(a.SourceId!=null&&receipt.EntityType is "Project" or "Task" or "Workstream" or "TrackingEntry")db.SourceLinks.Add(new SourceLink{SourceId=a.SourceId.Value,EntityType=receipt.EntityType,EntityId=entity.Id});}
        receipt.Status="Executed";receipt.ReviewedBy=db.Actor;receipt.ReviewedAtUtc=DateTime.UtcNow;receipt.AppliedAtUtc=DateTime.UtcNow;receipt.Summary="Đã thực hiện sau xác nhận trực tiếp của người dùng.";await db.SaveChangesAsync();await tx.CommitAsync();return receipt;
    }
    private Task Schedule(ActionArguments args,ActionReceipt receipt)=>ScheduleReminder(args,receipt);
    private async Task ScheduleReminder(ActionArguments args,ActionReceipt receipt)
    {
        var reminder=new Reminder{TaskId=args.TaskId!.Value,ScheduledAtUtc=DateTime.SpecifyKind(args.AtUtc!.Value,DateTimeKind.Utc),DedupeKey=receipt.IdempotencyKey};
        db.Reminders.Add(reminder);receipt.EntityType="Reminder";receipt.EntityId=reminder.Id;receipt.AppliedVersion=1;
        await Task.CompletedTask;
    }
    public async Task Undo(Guid id,int version)
    {
        await using var tx=await db.Database.BeginTransactionAsync();var r=await db.Actions.FindAsync(id)??throw new ValidationException("Không có hành động.");
        if(r.Version!=version||r.Status!="Executed"||r.UndoneAtUtc!=null||r.EntityId==null)throw new ValidationException("Không thể hoàn tác hành động này.");
        Record? entity=r.EntityType switch{"Workstream"=>await db.Workstreams.FindAsync(r.EntityId),"Task"=>await db.Tasks.FindAsync(r.EntityId),"Project"=>await db.Projects.FindAsync(r.EntityId),"TrackingEntry"=>await db.TrackingEntries.FindAsync(r.EntityId),"Source"=>await db.Sources.FindAsync(r.EntityId),"Reminder"=>await db.Reminders.FindAsync(r.EntityId),_=>null};
        if(entity==null||entity.Version!=r.AppliedVersion)throw new ValidationException("Dữ liệu đã thay đổi sau hành động; không hoàn tác ghi đè.");
        if(entity is Reminder reminder){reminder.Status="Cancelled";reminder.ArchivedAtUtc=DateTime.UtcNow;}
        else if(r.BeforeJson==null){entity.ArchivedAtUtc=DateTime.UtcNow;}
        else if(entity is WorkTask task){var before=JsonSerializer.Deserialize<WorkTask>(r.BeforeJson)!;task.Title=before.Title;task.Description=before.Description;task.OwnerText=before.OwnerText;task.NextAction=before.NextAction;task.Status=before.Status;task.Priority=before.Priority;task.DueDate=before.DueDate;task.DueDateType=before.DueDateType;task.CompletedAtUtc=before.CompletedAtUtc;}
        else if(entity is Project project){project.CurrentTargetDate=JsonSerializer.Deserialize<Project>(r.BeforeJson)!.CurrentTargetDate;}
        else throw new ValidationException("Không có hoàn tác an toàn cho loại này.");
        r.UndoneAtUtc=DateTime.UtcNow;r.Status="Undone";r.Summary="Đã hoàn tác bằng cập nhật bù; lịch sử vẫn còn.";await db.SaveChangesAsync();await tx.CommitAsync();
    }
}
