using AIWork.Domain;
using AIWork.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Xunit;
namespace AIWork.Tests;
public class MemoryTests
{
    [Fact]public async Task MeetingThreeActionsAreProposalsWithSourceAndNoDuplicates()
    {
        using var s=new TestStore();var inbox=new InboxService(s.Db,new TrackingService(s.Db));var key=Guid.NewGuid().ToString();var item=await inbox.Capture(SourceKind.Meeting,"Three actions","1. Check dimensions\n2. Ask supplier for schedule\n3. Review quality report",DateTime.UtcNow.AddDays(-2),null,true,null,key);
        var again=await inbox.Capture(SourceKind.Meeting,"retry","same capture",null,null,true,null,key);Assert.Equal(item.Id,again.Id);Assert.Equal(3,await inbox.ExtractChecklist(item.Id));Assert.Equal(0,await inbox.ExtractChecklist(item.Id));Assert.Equal(0,await s.Db.Tasks.CountAsync());
        var service=new ActionService(s.Db,new TrackingService(s.Db));foreach(var action in await s.Db.Actions.ToListAsync()){Assert.Equal(item.SourceId,action.OriginSourceId);await service.Approve(action.Id,action.Version);}Assert.Equal(3,await s.Db.Tasks.CountAsync());Assert.All(await s.Db.Tasks.ToListAsync(),t=>{Assert.Null(t.OwnerText);Assert.Null(t.DueDate);Assert.Equal(WorkStatus.Todo,t.Status);});Assert.Equal("NeedsReview",item.Status);
    }
    [Fact]public async Task ContradictionsRetainOldFactsAndCommittedTargetUntilUserVerifies()
    {
        using var s=new TestStore();var p=new Project{Code="LM",Name="LM Guide",CurrentTargetDate=new DateOnly(2026,11,15)};s.Db.Projects.Add(p);var tracking=new TrackingService(s.Db);var source=tracking.Capture(SourceKind.Email,"Supplier promise","We promise a changed drawing, not acceptance",DateTime.UtcNow.AddDays(-3),null,true);await s.Db.SaveChangesAsync();var memory=new MemoryService(s.Db,tracking);
        var first=await memory.Add(new MemoryFact{SubjectType="Project",SubjectId=p.Id,Predicate="frame_revision",ValueText="Drawing A",SourceId=source.Id,EffectiveAtUtc=DateTime.UtcNow.AddDays(-3)});await memory.Verify(first.Id,first.Version,"User checked drawing A");
        var second=await memory.Add(new MemoryFact{SubjectType="Project",SubjectId=p.Id,Predicate="frame_revision",ValueText="Drawing B",SourceId=source.Id,EffectiveAtUtc=DateTime.UtcNow.AddDays(-1)});Assert.Equal(Verification.Conflicted,second.Verification);Assert.Equal(Verification.Verified,first.Verification);Assert.Equal(first.Id,second.ConflictsWithId);Assert.Equal(new DateOnly(2026,11,15),p.CurrentTargetDate);
        await memory.Verify(second.Id,second.Version,"Explicitly verified drawing B");Assert.Equal(Verification.Superseded,first.Verification);Assert.False(first.IsCurrent);Assert.Equal(second.Id,first.SupersededById);Assert.True(await s.Db.MemoryRevisions.CountAsync()>=3);Assert.NotEqual(ProjectStatus.Completed,p.Status);
    }
    [Fact]public async Task AttachmentsRejectTraversalExecutableSpoofAndPreserveUnsupportedFiles()
    {
        using var s=new TestStore();var source=new TrackingService(s.Db).Capture(SourceKind.File,"Evidence","Attachment source",null,null,true);await s.Db.SaveChangesAsync();var files=new AttachmentService(s.Db,Path.Combine(s.Folder,"Attachments"));
        Assert.Throws<ValidationException>(()=>AttachmentService.Check("../bad.txt",Encoding.UTF8.GetBytes("x")));Assert.Throws<ValidationException>(()=>AttachmentService.Check("payload.exe",[77,90]));Assert.Throws<ValidationException>(()=>AttachmentService.Check("spoof.pdf",[77,90]));
        var pdf=await files.Store(source.Id,AttachmentService.Check("drawing.pdf",Encoding.ASCII.GetBytes("%PDF-1.4 synthetic")));await s.Db.SaveChangesAsync();Assert.Contains("Unsupported",pdf.ExtractionStatus);Assert.Null(pdf.ExtractedText);Assert.True(File.Exists(files.PathFor(pdf)));Assert.NotEqual("drawing.pdf",pdf.StoredName);
        var txt=await files.Store(source.Id,AttachmentService.Check("note.txt",Encoding.UTF8.GetBytes("<script>not executed</script>")));await s.Db.SaveChangesAsync();Assert.Contains("<script>",txt.ExtractedText);Assert.NotEmpty(txt.Sha256);
    }
}
