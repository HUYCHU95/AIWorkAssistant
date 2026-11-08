using AIWork.Application;
using AIWork.Domain;
using Microsoft.EntityFrameworkCore;
namespace AIWork.Infrastructure;
public record ProgressRow(Guid Id,string Name,string? Workstream,int TotalTasks,int DoneTasks,double? ActualPercent,double? PlannedPercent,double? Variance,string Health,string Status,DateOnly? Baseline,DateOnly? Target);
public class DashboardService(WorkDb db)
{
    public async Task<object> Snapshot(Guid? workstreamId=null,ProjectStatus? status=null,DateOnly? from=null,DateOnly? until=null)
    {
        var projects=await db.Projects.Where(p=>p.ArchivedAtUtc==null&&(workstreamId==null||p.WorkstreamId==workstreamId)&&(status==null||p.Status==status)).ToListAsync();
        var streams=await db.Workstreams.Where(w=>w.ArchivedAtUtc==null).ToDictionaryAsync(w=>w.Id,w=>w.Name);var ids=projects.Select(p=>p.Id).ToHashSet();
        var tasks=await db.Tasks.Where(t=>t.ArchivedAtUtc==null).ToListAsync();if(workstreamId!=null||status!=null)tasks=tasks.Where(t=>(t.ProjectId!=null&&ids.Contains(t.ProjectId.Value))||(status==null&&t.ProjectId==null&&t.WorkstreamId==workstreamId)).ToList();
        if(from!=null)tasks=tasks.Where(t=>t.DueDate>=from).ToList();if(until!=null)tasks=tasks.Where(t=>t.DueDate<=until).ToList();var today=WorkRules.Today;
        var progress=projects.Select(p=>
        {
            var scope=tasks.Where(t=>t.ProjectId==p.Id&&t.Status!=WorkStatus.Cancelled).ToList();var done=scope.Count(t=>t.Status==WorkStatus.Done);double? actual=scope.Count==0?null:Math.Round(100.0*done/scope.Count,1);
            double? planned=scope.Count>0&&scope.All(t=>t.DueDate!=null&&t.DueDateType is DeadlineType.Committed or DeadlineType.Planned)?Math.Round(100.0*scope.Count(t=>t.DueDate<=today)/scope.Count,1):null;
            return new ProgressRow(p.Id,p.Name,p.WorkstreamId is {} w?streams.GetValueOrDefault(w):null,scope.Count,done,actual,planned,actual!=null&&planned!=null?Math.Round(actual.Value-planned.Value,1):null,p.Health.ToString(),p.Status.ToString(),p.BaselineTargetDate,p.CurrentTargetDate);
        }).ToList();
        var taskIds=tasks.Select(t=>t.Id).ToHashSet();var dependencies=(await db.TaskDependencies.Where(d=>d.ArchivedAtUtc==null).ToListAsync()).Where(d=>taskIds.Contains(d.TaskId)&&taskIds.Contains(d.DependsOnTaskId)).Select(d=>new{d.TaskId,d.DependsOnTaskId});
        return new
        {
            generatedAtUtc=DateTime.UtcNow,today,workstreams=streams.Select(w=>new{id=w.Key,name=w.Value}),
            kpis=new{projects=projects.Count,active=projects.Count(p=>p.Status==ProjectStatus.Active),onTrack=projects.Count(p=>p.Health==Health.OnTrack),atRisk=projects.Count(p=>p.Health is Health.AtRisk or Health.Delayed or Health.Blocked),done=tasks.Count(t=>t.Status==WorkStatus.Done),overdue=tasks.Count(t=>t.IsOverdue(today)),waiting=tasks.Count(t=>t.Status==WorkStatus.Waiting),decisions=await db.TrackingEntries.CountAsync(e=>e.Kind==TrackingKind.Decision&&e.Status==TrackingStatus.Open&&e.ArchivedAtUtc==null&&(workstreamId==null||e.ProjectId!=null&&ids.Contains(e.ProjectId.Value)))},
            progress,health=projects.GroupBy(p=>p.Health).Select(g=>new{status=g.Key.ToString(),label=WorkRules.Label(g.Key),count=g.Count()}),
            tasks=tasks.Select(t=>new{t.Id,t.Title,t.ProjectId,project=t.ProjectId is {} id?projects.FirstOrDefault(p=>p.Id==id)?.Name:null,t.OwnerText,status=t.Status.ToString(),priority=t.Priority.ToString(),t.DueDate,dueDateType=t.DueDateType?.ToString(),t.NextAction,t.Version,overdue=t.IsOverdue(today),t.StartDate}),dependencies,
            recent=await db.AuditEvents.OrderByDescending(a=>a.OccurredAtUtc).Select(a=>new{a.Action,a.EntityType,a.OccurredAtUtc}).Take(6).ToListAsync(),
            note="Tiến độ thực tế = Done / việc chưa hủy. Kế hoạch chỉ tính khi mọi việc có hạn Planned/Committed; thiếu cơ sở trả null. Health Unknown không là OnTrack."
        };
    }
}
