using AIWork.Domain;
using AIWork.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.ComponentModel.DataAnnotations;
using Xunit;
namespace AIWork.Tests;
public sealed class TestStore:IDisposable
{
    public string Folder{get;}=Path.Combine(Path.GetTempPath(),"AIWorkV1-"+Guid.NewGuid());
    public DbContextOptions<WorkDb> Options{get;}
    public WorkDb Db{get;}
    public TestStore(bool migrate=true){Directory.CreateDirectory(Folder);Options=new DbContextOptionsBuilder<WorkDb>().UseSqlite($"Data Source={Path.Combine(Folder,"test.db")};Foreign Keys=True;Pooling=False").Options;Db=new WorkDb(Options){Actor="test-user"};if(migrate)Db.Database.Migrate();}
    public void Dispose(){Db.Dispose();Directory.Delete(Folder,true);}
}
public class TrackingTests
{
    [Fact]public async Task UpgradeFromV01TwicePreservesIdsAndAttachments()
    {
        using var s=new TestStore(false);var initial=s.Db.Database.GetMigrations().First();await s.Db.GetService<IMigrator>().MigrateAsync(initial);
        var project=Guid.NewGuid();var task=Guid.NewGuid();var now=DateTime.UtcNow;
        await s.Db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Projects (Id,Code,Name,Phase,Status,Health,CreatedAtUtc,UpdatedAtUtc,Version) VALUES ({project},'LEGACY','Legacy project',0,1,0,{now},{now},1)");
        await s.Db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Tasks (Id,Title,ProjectId,Status,Priority,CreatedAtUtc,UpdatedAtUtc,Version) VALUES ({task},'Legacy task',{project},0,1,{now},{now},1)");
        await s.Db.Database.ExecuteSqlRawAsync("INSERT INTO Users(Id,UserName,PasswordHash,FailedAttempts) VALUES(1,'legacy-user','test-only-legacy-hash',0)");
        var attachment=Path.Combine(s.Folder,"original.txt");await File.WriteAllTextAsync(attachment,"preserve evidence");
        await s.Db.Database.MigrateAsync();await s.Db.Database.MigrateAsync();Assert.Equal(project,(await s.Db.Projects.SingleAsync()).Id);Assert.Equal(task,(await s.Db.Tasks.SingleAsync()).Id);Assert.Equal("preserve evidence",await File.ReadAllTextAsync(attachment));Assert.Equal("test-only-legacy-hash",(await s.Db.Users.SingleAsync()).PasswordHash);
    }
    [Fact]public async Task TrackingSourcesAndDependenciesAreValidatedAuditedAndImmutable()
    {
        using var s=new TestStore();var service=new TrackingService(s.Db);var a=new WorkTask{Title="A"};var b=new WorkTask{Title="B"};var c=new WorkTask{Title="C"};s.Db.Tasks.AddRange(a,b,c);await s.Db.SaveChangesAsync();
        await service.AddDependency(a.Id,b.Id);await s.Db.SaveChangesAsync();await service.AddDependency(b.Id,c.Id);await s.Db.SaveChangesAsync();await Assert.ThrowsAsync<ValidationException>(()=>service.AddDependency(c.Id,a.Id));await Assert.ThrowsAsync<ValidationException>(()=>service.AddDependency(a.Id,a.Id));
        var source=service.Capture(SourceKind.Email,"Supplier says will deliver","A promise, not acceptance",DateTime.UtcNow,null,true);await s.Db.SaveChangesAsync();
        foreach(var kind in Enum.GetValues<TrackingKind>()){var item=new TrackingEntry{Kind=kind,Title=kind.ToString(),Content="Reported only",SourceId=source.Id,TaskId=a.Id,Status=TrackingStatus.Waiting};TrackingService.Validate(item);s.Db.TrackingEntries.Add(item);}await s.Db.SaveChangesAsync();Assert.Equal(4,await s.Db.TrackingEntries.CountAsync());
        await service.Link(source.Id,"Task",a.Id);await s.Db.SaveChangesAsync();await service.Link(source.Id,"Task",a.Id);await s.Db.SaveChangesAsync();Assert.Equal(1,await s.Db.SourceLinks.CountAsync());await Assert.ThrowsAsync<ValidationException>(()=>service.Link(source.Id,"Task",Guid.NewGuid()));
        Assert.All(await s.Db.TrackingEntries.ToListAsync(),e=>Assert.Equal(Verification.Reported,e.Verification));Assert.True(await s.Db.AuditEvents.AnyAsync(e=>e.EntityType=="TrackingEntry"));
        source.OriginalText="tampered";await Assert.ThrowsAsync<InvalidOperationException>(()=>s.Db.SaveChangesAsync());
    }
}


