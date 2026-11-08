using AIWork.Infrastructure;
using AIWork.Domain;
using Microsoft.EntityFrameworkCore;
namespace AIWork.Web;
public class PlannerWorker(IServiceScopeFactory scopes):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WorkDb>();if(!await db.Users.AnyAsync(stoppingToken))continue;
                await scope.ServiceProvider.GetRequiredService<PlannerService>().RunOnce(DateTime.UtcNow,stoppingToken);
                var p=await db.Preferences.AsNoTracking().SingleOrDefaultAsync(stoppingToken);if(p?.SmtpEnabled==true&&p.SmtpAutomaticReminders&&p.SmtpVerifiedRecipient==p.SmtpRecipient&&!string.IsNullOrWhiteSpace(p.SmtpRecipient))
                {
                    var pending=await db.Notifications.Where(n=>n.ReadAtUtc==null&&n.DismissedAtUtc==null&&!db.Deliveries.Any(d=>d.NotificationId==n.Id)).OrderBy(n=>n.CreatedAtUtc).Take(3).Select(n=>n.Id).ToListAsync(stoppingToken);
                    foreach(var id in pending)await scope.ServiceProvider.GetRequiredService<MailService>().Send(id,p.SmtpRecipient,false,stoppingToken);
                }
            }
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex)
            {
                try{using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<WorkDb>();var health=await db.WorkerHealth.SingleOrDefaultAsync(stoppingToken);if(health==null){health=new WorkerHealth();db.WorkerHealth.Add(health);}health.LastErrorCode=ex.GetType().Name+"; no credentials logged";await db.SaveChangesAsync(stoppingToken);}catch(OperationCanceledException){break;}catch{ /* Retry next bounded tick; never reset DB. */ }
            }
        }while(await timer.WaitForNextTickAsync(stoppingToken));
    }
}
