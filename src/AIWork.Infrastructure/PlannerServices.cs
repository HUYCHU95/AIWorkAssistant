using AIWork.Application;
using AIWork.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
namespace AIWork.Infrastructure;
public class SmtpTransport(WorkDb db,ISecretStore secrets):IMailTransport
{
    public async Task Send(MailEnvelope envelope,CancellationToken ct)
    {
        var p=await db.Preferences.AsNoTracking().SingleOrDefaultAsync(ct)??throw new ValidationException("SMTP chưa cấu hình.");
        if(!p.SmtpEnabled||!p.SmtpTls||string.IsNullOrWhiteSpace(p.SmtpHost))throw new ValidationException("SMTP chưa bật TLS/cấu hình.");
        using var smtp=new SmtpClient(p.SmtpHost,p.SmtpPort){EnableSsl=true,DeliveryMethod=SmtpDeliveryMethod.Network,UseDefaultCredentials=false,Timeout=20000};
        if(!string.IsNullOrWhiteSpace(p.SmtpUser)){var secret=secrets.Read("smtp");if(string.IsNullOrWhiteSpace(secret))throw new ValidationException("Thiếu mật khẩu SMTP; nhập lại khi chuyển máy.");smtp.Credentials=new NetworkCredential(p.SmtpUser,secret);}
        using var mail=new MailMessage(envelope.From,envelope.To,envelope.Subject,envelope.Body){IsBodyHtml=false};await smtp.SendMailAsync(mail,ct);
    }
}
public class MailService(WorkDb db,IMailTransport transport)
{
    public async Task<NotificationDelivery> Send(Guid? notificationId,string confirmedRecipient,bool test,CancellationToken ct)
    {
        var p=await db.Preferences.SingleOrDefaultAsync(ct)??throw new ValidationException("Chưa cấu hình SMTP.");
        if(!p.SmtpEnabled||!p.SmtpTls||string.IsNullOrWhiteSpace(p.SmtpFrom)||string.IsNullOrWhiteSpace(p.SmtpRecipient)||!string.Equals(p.SmtpRecipient,confirmedRecipient,StringComparison.OrdinalIgnoreCase))throw new ValidationException("Cần cấu hình TLS và xác nhận đúng người nhận.");
        var key=test?"test:"+Guid.NewGuid():"mail:"+notificationId;var old=await db.Deliveries.SingleOrDefaultAsync(d=>d.DedupeKey==key,ct);if(old!=null)return old;
        var notification=notificationId==null?null:await db.Notifications.FindAsync([notificationId],ct);if(!test&&notification==null)throw new ValidationException("Không có thông báo.");
        var d=new NotificationDelivery{NotificationId=notificationId,DedupeKey=key};db.Deliveries.Add(d);await db.SaveChangesAsync(ct);
        try
        {
            await transport.Send(new MailEnvelope(p.SmtpFrom,p.SmtpRecipient,test?"AIWork — kiểm tra SMTP":"AIWork — "+notification!.Title,test?"Email kiểm thử do chính bạn xác nhận. Không có dữ liệu công ty.":notification!.Body),ct);
            d.Result="Sent";if(test)p.SmtpVerifiedRecipient=p.SmtpRecipient;
        }
        catch(Exception ex)when(ex is SmtpException or HttpRequestException or OperationCanceledException or ValidationException or InvalidOperationException)
        {d.Result=ex is ValidationException?"Failed":"Unknown";d.ErrorCode=ex.GetType().Name+" — inspect configuration; no automatic resend";}
        await db.SaveChangesAsync(CancellationToken.None);return d;
    }
}
public class PlannerService(WorkDb db)
{
    public async Task RunOnce(DateTime now,CancellationToken ct=default)
    {
        now=DateTime.SpecifyKind(now,DateTimeKind.Utc);var p=await db.Preferences.AsNoTracking().SingleOrDefaultAsync(ct)??new AppPreferences();if(!p.WorkerEnabled)return;
        var priorActor=db.Actor;db.Actor="System/Planner";
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var pending=await db.Reminders.Where(r=>r.Status=="Pending"&&r.ArchivedAtUtc==null&&r.ScheduledAtUtc<=now).OrderBy(r=>r.ScheduledAtUtc).Take(50).ToListAsync(ct);
        var asked=new HashSet<Guid>();
        foreach(var r in pending)
        {
            var task=await db.Tasks.FindAsync([r.TaskId],ct);r.LastAttemptAtUtc=now;
            if(task==null||task.ArchivedAtUtc!=null||task.Status is WorkStatus.Done or WorkStatus.Cancelled){r.Status="Cancelled";continue;}
            if(asked.Contains(task.Id)){r.Status="DeliveredInApp";continue;}
            var key="reminder:"+r.Id;if(!await db.Notifications.AnyAsync(n=>n.DedupeKey==key,ct))db.Notifications.Add(new Notification{DedupeKey=key,Title="Đến lịch theo dõi: "+task.Title,Body=$"{task.Title}\n{WorkRules.Label(task.Status)}\nHạn {WorkRules.Date(task.DueDate)} — {WorkRules.Label(task.DueDateType)}\nBước tiếp: {task.NextAction??"Chưa ghi"}\nNguồn: /Tasks/Details/{task.Id}",TaskId=task.Id,ReminderId=r.Id});
            r.Status="DeliveredInApp";task.LastAskedAtUtc=now;task.LastAskedVersion=task.Version+1;asked.Add(task.Id);
        }
        var local=TimeZoneInfo.ConvertTimeFromUtc(now,WorkRules.Vietnam);var today=DateOnly.FromDateTime(local);var slot=local.Hour>=p.EveningHour?"evening":local.Hour>=p.MorningHour?"morning":null;
        if(slot!=null)
        {
            var key=$"brief:{today:yyyyMMdd}:{slot}";if(!await db.Notifications.AnyAsync(n=>n.DedupeKey==key,ct))
            {
                var all=await db.Tasks.Where(t=>t.ArchivedAtUtc==null&&t.Status!=WorkStatus.Done&&t.Status!=WorkStatus.Cancelled&&(t.NextCheckAtUtc==null||t.NextCheckAtUtc<=now)).ToListAsync(ct);
                var chosen=all.Where(t=>!asked.Contains(t.Id)&&(t.LastAskedAtUtc==null||t.LastAskedVersion<t.Version||t.LastAskedAtUtc<=now.AddDays(-7))&&(t.DueDate<=today.AddDays(7)||t.Status is WorkStatus.Waiting or WorkStatus.Blocked||t.NextCheckAtUtc<=now)).OrderByDescending(t=>t.IsOverdue(today)).ThenByDescending(t=>t.Status==WorkStatus.Blocked).ThenByDescending(t=>t.Priority).ThenBy(t=>t.DueDate??DateOnly.MaxValue).Take(5).ToList();
                if(chosen.Count>0)
                {
                    var body="Work Briefing (rules-based) — cần cập nhật các mục sau:\n"+string.Join("\n",chosen.Select(t=>$"• {t.Title}: {WorkRules.Label(t.Status)}, {WorkRules.Date(t.DueDate)} ({WorkRules.Label(t.DueDateType)}) — /Tasks/Details/{t.Id}"));
                    db.Notifications.Add(new Notification{DedupeKey=key,Title=slot=="morning"?"Ưu tiên buổi sáng":"Cập nhật cuối ngày",Body=body});
                    foreach(var task in chosen){task.LastAskedAtUtc=now;task.LastAskedVersion=task.Version+1;}
                }
            }
            if(local.DayOfWeek==DayOfWeek.Friday&&slot=="evening")
            {
                var keyWeekly=$"weekly:{today:yyyyMMdd}";if(!await db.Notifications.AnyAsync(n=>n.DedupeKey==keyWeekly,ct)){var tasks=await db.Tasks.Where(t=>t.ArchivedAtUtc==null).ToListAsync(ct);db.Notifications.Add(new Notification{DedupeKey=keyWeekly,Title="Tổng kết tuần (rules-based)",Body=$"{tasks.Count} công việc; {tasks.Count(t=>t.Status==WorkStatus.Done)} hoàn thành; {tasks.Count(t=>t.IsOverdue(today))} quá hạn. Không tự xác minh dự án hoàn thành."});}
            }
        }
        var health=await db.WorkerHealth.SingleOrDefaultAsync(ct);if(health==null){health=new WorkerHealth();db.WorkerHealth.Add(health);}health.LastRunAtUtc=now;health.LastErrorCode=null;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);db.Actor=priorActor;
    }
    public async Task Snooze(Guid notificationId,int version,DateTime until)
    {
        if(until<=DateTime.UtcNow||until>DateTime.UtcNow.AddDays(90))throw new ValidationException("Chọn thời điểm trong 90 ngày tới.");var n=await db.Notifications.FindAsync(notificationId)??throw new ValidationException("Không có thông báo.");if(n.Version!=version)throw new ValidationException("Thông báo đã thay đổi.");
        if(n.TaskId==null)throw new ValidationException("Briefing tổng hợp không thể hoãn toàn bộ; hãy hoãn từng công việc trong Planner.");
        n.SnoozedUntilUtc=DateTime.SpecifyKind(until,DateTimeKind.Utc);n.ReadAtUtc=DateTime.UtcNow;var task=await db.Tasks.FindAsync(n.TaskId);if(task!=null)task.NextCheckAtUtc=n.SnoozedUntilUtc;
        var key=$"snooze:{n.Id}:{until.Ticks}";if(!await db.Reminders.AnyAsync(r=>r.DedupeKey==key))db.Reminders.Add(new Reminder{TaskId=n.TaskId.Value,ScheduledAtUtc=n.SnoozedUntilUtc.Value,DedupeKey=key});await db.SaveChangesAsync();
    }
}

