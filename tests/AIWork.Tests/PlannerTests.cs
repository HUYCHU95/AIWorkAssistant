using AIWork.Domain;
using AIWork.Infrastructure;
using AIWork.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Xunit;
namespace AIWork.Tests;
public class FakeMailTransport:IMailTransport
{
    public int Sends;public bool Fail;public Task Send(MailEnvelope envelope,CancellationToken ct){Sends++;if(Fail)throw new System.Net.Mail.SmtpException("test sink failure");return Task.CompletedTask;}
}
public class PlannerTests
{
    [Fact]public async Task CatchUpDedupeSnoozeAndAdaptiveNoSpamSurviveRestart()
    {
        using var s=new TestStore();var now=DateTime.SpecifyKind(new DateTime(2026,11,9,2,0,0),DateTimeKind.Utc);var task=new WorkTask{Title="Supplier promise",Status=WorkStatus.Waiting,DueDate=new DateOnly(2026,11,10),DueDateType=DeadlineType.Suggested};var suppressed=new WorkTask{Title="Do not ask yet",NextCheckAtUtc=now.AddDays(2),Status=WorkStatus.Blocked};s.Db.Tasks.AddRange(task,suppressed);var reminder=new Reminder{TaskId=task.Id,ScheduledAtUtc=now.AddDays(-2),DedupeKey="test-catchup"};s.Db.Reminders.Add(reminder);await s.Db.SaveChangesAsync();
        await new PlannerService(s.Db).RunOnce(now);Assert.Equal("DeliveredInApp",reminder.Status);Assert.Equal(1,await s.Db.Notifications.CountAsync());Assert.DoesNotContain("Do not ask yet",(await s.Db.Notifications.SingleAsync()).Body);
        using(var restarted=new WorkDb(s.Options)){await new PlannerService(restarted).RunOnce(now.AddMinutes(1));Assert.Equal(1,await restarted.Notifications.CountAsync());}
        var notification=await s.Db.Notifications.SingleAsync();var snooze=DateTime.UtcNow.AddDays(2);await new PlannerService(s.Db).Snooze(notification.Id,notification.Version,snooze);Assert.Equal(snooze,task.NextCheckAtUtc);Assert.Equal(2,await s.Db.Reminders.CountAsync());
        await new PlannerService(s.Db).RunOnce(now.AddHours(8));Assert.Equal(1,await s.Db.Notifications.CountAsync());Assert.NotEqual(WorkStatus.Done,task.Status);
    }
    [Fact]public async Task SmtpRequiresExactRecipientAndNeverPretendsFailedSendSucceeded()
    {
        using var s=new TestStore();var fake=new FakeMailTransport();var mail=new MailService(s.Db,fake);s.Db.Preferences.Add(new AppPreferences{SmtpEnabled=true,SmtpTls=true,SmtpHost="test.invalid",SmtpFrom="sender@example.invalid",SmtpRecipient="recipient@example.invalid"});var n=new Notification{DedupeKey="mail-test",Title="Synthetic reminder",Body="No company data"};s.Db.Notifications.Add(n);await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ValidationException>(()=>mail.Send(n.Id,"wrong@example.invalid",false,default));Assert.Equal(0,fake.Sends);var sent=await mail.Send(n.Id,"recipient@example.invalid",false,default);Assert.Equal("Sent",sent.Result);await mail.Send(n.Id,"recipient@example.invalid",false,default);Assert.Equal(1,fake.Sends);
        fake.Fail=true;var failed=await mail.Send(null,"recipient@example.invalid",true,default);Assert.Equal("Unknown",failed.Result);Assert.DoesNotContain("password",failed.ErrorCode??"");Assert.Null((await s.Db.Preferences.SingleAsync()).SmtpVerifiedRecipient);
    }
    [Fact]public async Task BackupRestoreValidatesHashesRetainsOriginalAndDisablesExternalProcessing()
    {
        using var s=new TestStore();var data=s.Folder;Directory.CreateDirectory(Path.Combine(data,"Attachments"));Directory.CreateDirectory(Path.Combine(data,"Backups"));Directory.CreateDirectory(Path.Combine(data,"Logs"));var file=Path.Combine(data,"Attachments","evidence.txt");await File.WriteAllTextAsync(file,"original attachment");var task=new WorkTask{Title="Before restore"};s.Db.Tasks.Add(task);s.Db.Preferences.Add(new AppPreferences{AIEnabled=true,AllowExternalAIProcessing=true,Model="test",SmtpEnabled=true,SmtpAutomaticReminders=true,SmtpVerifiedRecipient="x@example.invalid"});await s.Db.SaveChangesAsync();
        var backup=await new BackupService(s.Db,data).Create();var stage=Path.Combine(data,"validation");var manifest=BackupService.Validate(backup,stage,s.Db.Database.GetMigrations().ToHashSet());Assert.Equal(2,manifest.Files.Count);Assert.True(manifest.Files.All(f=>f.Sha256.Length==64));
        task.Title="After backup";await s.Db.SaveChangesAsync();await File.WriteAllTextAsync(file,"changed attachment");await s.Db.Database.CloseConnectionAsync();
        // Offline destination mirrors a live installation and has separate current files.
        var destination=Path.Combine(data,"destination");Directory.CreateDirectory(Path.Combine(destination,"RestoreQueue"));Directory.CreateDirectory(Path.Combine(destination,"Attachments"));Directory.CreateDirectory(Path.Combine(destination,"Backups"));Directory.CreateDirectory(Path.Combine(destination,"Logs"));
        File.Copy(Path.Combine(data,"test.db"),Path.Combine(destination,"AIWork.db"));await File.WriteAllTextAsync(Path.Combine(destination,"Attachments","evidence.txt"),"changed attachment");var token=Guid.NewGuid().ToString("N");Directory.CreateDirectory(Path.Combine(destination,"RestoreQueue",token));File.Copy(backup,Path.Combine(destination,"RestoreQueue",token,"package.zip"));await File.WriteAllTextAsync(Path.Combine(destination,"RestoreQueue","request.json"),JsonSerializer.Serialize(new PendingRestore(token,BackupService.HashFile(backup),"test-user")));
        BackupService.ApplyPendingOffline(destination,s.Db.Database.GetMigrations().ToHashSet());using var restored=new WorkDb(new DbContextOptionsBuilder<WorkDb>().UseSqlite($"Data Source={Path.Combine(destination,"AIWork.db")};Pooling=False").Options);Assert.Equal("Before restore",(await restored.Tasks.SingleAsync()).Title);Assert.False((await restored.Preferences.SingleAsync()).AllowExternalAIProcessing);Assert.False((await restored.Preferences.SingleAsync()).SmtpEnabled);Assert.Equal("original attachment",await File.ReadAllTextAsync(Path.Combine(destination,"Attachments","evidence.txt")));Assert.True(File.Exists(Path.Combine(destination,"Backups","pre-restore-files-"+token,"AIWork.db")));
        // Corruption is rejected before touching destination.
        var corrupt=Path.Combine(data,"corrupt.zip");File.Copy(backup,corrupt);using(var zip=ZipFile.Open(corrupt,ZipArchiveMode.Update)){zip.GetEntry("Data/Attachments/evidence.txt")!.Delete();var entry=zip.CreateEntry("Data/Attachments/evidence.txt");using var writer=new StreamWriter(entry.Open());writer.Write("tampered");}Assert.Throws<ValidationException>(()=>BackupService.Validate(corrupt,Path.Combine(data,"bad"),s.Db.Database.GetMigrations().ToHashSet()));
        var traversal=Path.Combine(data,"traversal.zip");using(var zip=ZipFile.Open(traversal,ZipArchiveMode.Create)){zip.CreateEntry("../escape.txt");}Assert.Throws<ValidationException>(()=>BackupService.Validate(traversal,Path.Combine(data,"bad2"),s.Db.Database.GetMigrations().ToHashSet()));
    }
    [Fact]public async Task OfflineRestoreRollsBackDatabaseWhenAttachmentSwapIsLocked()
    {
        if(!OperatingSystem.IsWindows())return;
        using var s=new TestStore();Directory.CreateDirectory(Path.Combine(s.Folder,"Attachments"));Directory.CreateDirectory(Path.Combine(s.Folder,"Logs"));var task=new WorkTask{Title="Snapshot value"};s.Db.Tasks.Add(task);await s.Db.SaveChangesAsync();var package=await new BackupService(s.Db,s.Folder).Create();task.Title="Current value must survive failed restore";await s.Db.SaveChangesAsync();await s.Db.Database.CloseConnectionAsync();
        var destination=Path.Combine(s.Folder,"locked-destination");Directory.CreateDirectory(Path.Combine(destination,"Attachments"));Directory.CreateDirectory(Path.Combine(destination,"Backups"));Directory.CreateDirectory(Path.Combine(destination,"Logs"));File.Copy(Path.Combine(s.Folder,"test.db"),Path.Combine(destination,"AIWork.db"));var lockedFile=Path.Combine(destination,"Attachments","held.txt");await File.WriteAllTextAsync(lockedFile,"do not lose");var token=Guid.NewGuid().ToString("N");var queue=Path.Combine(destination,"RestoreQueue",token);Directory.CreateDirectory(queue);File.Copy(package,Path.Combine(queue,"package.zip"));await File.WriteAllTextAsync(Path.Combine(destination,"RestoreQueue","request.json"),JsonSerializer.Serialize(new PendingRestore(token,BackupService.HashFile(package),"test-user")));
        using(var held=new FileStream(lockedFile,FileMode.Open,FileAccess.Read,FileShare.None))Assert.ThrowsAny<IOException>(()=>BackupService.ApplyPendingOffline(destination,s.Db.Database.GetMigrations().ToHashSet()));
        using var current=new WorkDb(new DbContextOptionsBuilder<WorkDb>().UseSqlite($"Data Source={Path.Combine(destination,"AIWork.db")};Pooling=False").Options);Assert.Equal("Current value must survive failed restore",(await current.Tasks.SingleAsync()).Title);Assert.Equal("do not lose",await File.ReadAllTextAsync(lockedFile));
    }
    [Fact]public async Task MigrationProbePreservesLegacyRowsAndCreatesSnapshot()
    {
        using var s=new TestStore(false);var first=s.Db.Database.GetMigrations().First();await s.Db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync(first);var id=Guid.NewGuid();var now=DateTime.UtcNow;await s.Db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Projects(Id,Code,Name,Phase,Status,Health,CreatedAtUtc,UpdatedAtUtc,Version)VALUES({id},'OLD','Old data',0,0,0,{now},{now},1)");await MigrationSafety.Upgrade(s.Db,s.Folder);Assert.Equal(id,(await s.Db.Projects.SingleAsync()).Id);Assert.True(Directory.GetFiles(Path.Combine(s.Folder,"Backups"),"pre-migration-*.db").Length==1);
    }
}


