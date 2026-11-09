using AIWork.Application;
using AIWork.Domain;
using AIWork.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
namespace AIWork.Tests;
public class ExperienceTests
{
    [Fact]public async Task ProjectChatToolExposesCodeAndPersistsOnlyAfterApproval()
    {
        using var s=new TestStore();var tracking=new TrackingService(s.Db);var actions=new ActionService(s.Db,tracking);var tools=new ControlledTools(new ReadTools(s.Db),actions);var schema=tools.Definitions.Single(t=>t?["name"]?.GetValue<string>()=="create_project");Assert.NotNull(schema?["parameters"]?["properties"]?["code"]);
        var conversation=new Conversation();var message=new ChatMessage{ConversationId=conversation.Id,Role="user",Content="Create synthetic project"};s.Db.Conversations.Add(conversation);s.Db.Messages.Add(message);await s.Db.SaveChangesAsync();var output=await tools.Run("create_project","{\"code\":\"CHAT-P\",\"title\":\"Synthetic project\",\"body\":\"Review only\"}",conversation,message.Id,default);Assert.Contains("PendingApproval",output);Assert.Empty(await s.Db.Projects.ToListAsync());var receipt=await s.Db.Actions.SingleAsync();await actions.Approve(receipt.Id,receipt.Version);var project=await s.Db.Projects.SingleAsync();Assert.Equal("CHAT-P",project.Code);Assert.Equal(Health.Unknown,project.Health);Assert.Null(project.CurrentTargetDate);
    }
    [Fact]public void ValidPdfTextIsExtractedAndLongPdfIsBounded()
    {
        using var builder=new UglyToad.PdfPig.Writer.PdfDocumentBuilder();var font=builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);builder.AddPage(600,800).AddText("Synthetic PDF evidence",16,new UglyToad.PdfPig.Core.PdfPoint(20,700),font);var content=DocumentExtraction.Read(AttachmentService.Check("evidence.pdf",builder.Build()));Assert.Contains("Synthetic PDF evidence",content.Text);Assert.Equal(1,content.Pages);
        using var longBuilder=new UglyToad.PdfPig.Writer.PdfDocumentBuilder();for(var i=0;i<21;i++)longBuilder.AddPage(600,800);var bounded=DocumentExtraction.Read(AttachmentService.Check("long.pdf",longBuilder.Build()));Assert.Contains("exceeds 20-page",bounded.Status);
    }
    private class CancelProvider(CancellationTokenSource stop):IAIProvider
    {
        public Task<ProviderResponse> Respond(string model,JsonArray input,JsonArray tools,CancellationToken ct)=>throw new NotSupportedException();
        public async Task<ProviderResponse> RespondStreaming(string model,JsonArray input,JsonArray tools,Func<string,Task> delta,CancellationToken ct){await delta("Partial test response");stop.Cancel();ct.ThrowIfCancellationRequested();throw new NotSupportedException();}
    }
    [Fact]public async Task CancelledChatStoresPartialAndCannotRepeatWrites()
    {
        using var s=new TestStore();var c=new Conversation();s.Db.Conversations.Add(c);s.Db.Preferences.Add(new AppPreferences{AIEnabled=true,AllowExternalAIProcessing=true,Model="test"});await s.Db.SaveChangesAsync();var secrets=new MemorySecrets();secrets.Write("openai","test-only");using var stop=new CancellationTokenSource();var chat=new ChatService(s.Db,new CancelProvider(stop),new ReadTools(s.Db),secrets);var request=Guid.NewGuid();await chat.Send(c.Id,"cancel synthetic",stop.Token,request,(_,_)=>Task.CompletedTask);Assert.Equal("Cancelled",(await s.Db.ChatTurns.SingleAsync()).Status);Assert.True(await s.Db.Messages.AnyAsync(m=>m.Role=="assistant"&&m.Content=="Partial test response"&&m.IsError));await chat.Send(c.Id,"retry same",default,request);Assert.Equal(1,await s.Db.Messages.CountAsync(m=>m.Role=="user"));
    }
    [Fact]public async Task SourceWireFormatIncludesImagesAndPdfWithoutPrivateData()
    {
        using var s=new TestStore();var source=new TrackingService(s.Db).Capture(SourceKind.File,"Public synthetic","Public data",null,null,false);await s.Db.SaveChangesAsync();var files=new AttachmentService(s.Db,Path.Combine(s.Folder,"Attachments"));await files.Store(source.Id,AttachmentService.Check("test.png",[137,80,78,71,13,10,26,10,0]));await files.Store(source.Id,AttachmentService.Check("test.pdf",Encoding.ASCII.GetBytes("%PDF-1.4 synthetic")));await s.Db.SaveChangesAsync();var json=(await new SourceContentService(s.Db,files).Parts(source.Id,default)).ToJsonString();Assert.Contains("input_image",json);Assert.Contains("data:image/png;base64,",json);Assert.Contains("input_file",json);Assert.Contains("data:application/pdf;base64,",json);
    }
    [Fact]public async Task DashboardNeverInventsPlanOrHealthAndRespectsScope()
    {
        using var s=new TestStore();var stream=new Workstream{Code="S",Name="Stream"};s.Db.Workstreams.Add(stream);var p=new Project{Code="A",Name="Project",WorkstreamId=stream.Id};s.Db.Projects.Add(p);s.Db.Tasks.AddRange(new WorkTask{Title="Done",ProjectId=p.Id,Status=WorkStatus.Done},new WorkTask{Title="No plan",ProjectId=p.Id});await s.Db.SaveChangesAsync();
        JsonElement Snapshot(object v)=>JsonSerializer.SerializeToElement(v,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});var data=Snapshot(await new DashboardService(s.Db).Snapshot());var progress=data.GetProperty("progress")[0];Assert.Equal(50,progress.GetProperty("actualPercent").GetDouble());Assert.Equal(JsonValueKind.Null,progress.GetProperty("plannedPercent").ValueKind);Assert.Equal("Unknown",progress.GetProperty("health").GetString());Assert.Equal(0,data.GetProperty("kpis").GetProperty("onTrack").GetInt32());
        data=Snapshot(await new DashboardService(s.Db).Snapshot(Guid.NewGuid()));Assert.Empty(data.GetProperty("progress").EnumerateArray());
    }
    [Fact]public async Task PrivateSourceCannotReachProviderAndDuplicateAttachmentsAreReused()
    {
        using var s=new TestStore();var tracking=new TrackingService(s.Db);var source=tracking.Capture(SourceKind.File,"Private","Never transmit",null,null,true);await s.Db.SaveChangesAsync();var files=new AttachmentService(s.Db,Path.Combine(s.Folder,"Attachments"));var file=AttachmentService.Check("note.txt",Encoding.UTF8.GetBytes("Evidence"));var first=await files.Store(source.Id,file);await s.Db.SaveChangesAsync();Assert.Equal(first.Id,(await files.Store(source.Id,file)).Id);Assert.Single(Directory.GetFiles(Path.Combine(s.Folder,"Attachments")));
        var provider=new FakeAIProvider();var secrets=new MemorySecrets();secrets.Write("openai","test-secret");s.Db.Preferences.Add(new AppPreferences{AIEnabled=true,AllowExternalAIProcessing=true,Model="test-model"});var draft=new AnalysisDraft{SourceId=source.Id,RequestKey=Guid.NewGuid().ToString()};s.Db.AnalysisDrafts.Add(draft);await s.Db.SaveChangesAsync();var content=new SourceContentService(s.Db,files);var chat=new ChatService(s.Db,provider,new ReadTools(s.Db),secrets,content);var service=new AnalysisService(s.Db,provider,chat,content,tracking,new ActionService(s.Db,tracking));await service.Analyze(draft.Id,default);Assert.Equal("Blocked",draft.Status);Assert.Equal(0,provider.Count);await Assert.ThrowsAsync<ValidationException>(()=>content.Parts(source.Id,default));
    }
    [Fact]public void LocalWordAndSpreadsheetExtractionPreservesTextWithoutEvaluatingFormula()
    {
        using var ms=new MemoryStream();using(var zip=new ZipArchive(ms,ZipArchiveMode.Create,true)){void Entry(string name,string text){using var writer=new StreamWriter(zip.CreateEntry(name).Open());writer.Write(text);}Entry("[Content_Types].xml","<Types/>");Entry("word/document.xml","<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body><w:p><w:r><w:t>Supplier promises delivery, not acceptance.</w:t></w:r></w:p></w:body></w:document>");}var doc=DocumentExtraction.Read(AttachmentService.Check("meeting.docx",ms.ToArray()));Assert.Contains("not acceptance",doc.Text);
        using var book=new ClosedXML.Excel.XLWorkbook();var sheet=book.AddWorksheet("Plan");sheet.Cell("A1").Value="Task";sheet.Cell("B1").FormulaA1="1+1";using var xls=new MemoryStream();book.SaveAs(xls);var extracted=DocumentExtraction.Read(AttachmentService.Check("plan.xlsx",xls.ToArray()));Assert.Contains("Task",extracted.Text);Assert.Contains("formula not executed",extracted.Text);
    }
    [Fact]public async Task AnalysisNormalizesSuggestionsAndCreatesReviewedIdempotentActions()
    {
        using var s=new TestStore();var tracking=new TrackingService(s.Db);var source=tracking.Capture(SourceKind.File,"Public synthetic","Review source",null,null,false);await s.Db.SaveChangesAsync();var files=new AttachmentService(s.Db,Path.Combine(s.Folder,"Attachments"));var fake=new FakeAIProvider();var secrets=new MemorySecrets();var service=new AnalysisService(s.Db,fake,new ChatService(s.Db,fake,new ReadTools(s.Db),secrets),new SourceContentService(s.Db,files),tracking,new ActionService(s.Db,tracking));
        var result=new AnalysisResult{Fields=new(){["Status"]="Done",["DueDate"]="2026-11-20",["DueDateType"]="Committed",["ProjectId"]=Guid.NewGuid().ToString()},Items=[new(){Kind="Task",Title="Review",DueDate=new(2026,11,20),DueDateType="Committed",Confidence=.8}]};await service.Normalize(result,"Task",source.Id);Assert.False(result.Fields.ContainsKey("Status"));Assert.False(result.Fields.ContainsKey("ProjectId"));Assert.Equal("2",result.Fields["DueDateType"]);Assert.Equal("Suggested",result.Items[0].DueDateType);
        var draft=new AnalysisDraft{SourceId=source.Id,RequestKey=Guid.NewGuid().ToString(),Status="Ready"};s.Db.AnalysisDrafts.Add(draft);await s.Db.SaveChangesAsync();await service.Proposals(draft.Id,result.Items);await service.Proposals(draft.Id,result.Items);Assert.Equal(0,await s.Db.Tasks.CountAsync());var receipt=await s.Db.Actions.SingleAsync();Assert.Equal("PendingApproval",receipt.Status);await new ActionService(s.Db,tracking).Approve(receipt.Id,receipt.Version);var task=await s.Db.Tasks.SingleAsync();Assert.Equal(DeadlineType.Suggested,task.DueDateType);Assert.Null(task.OwnerText);await service.Proposals(draft.Id,result.Items);Assert.Equal(1,await s.Db.Tasks.CountAsync());Assert.Single(await s.Db.SourceLinks.Where(l=>l.EntityType=="Task").ToListAsync());
    }
    [Fact]public async Task ChatRequestIsIdempotentAndStreamingEventsAreStored()
    {
        using var s=new TestStore();var c=new Conversation();s.Db.Conversations.Add(c);s.Db.Preferences.Add(new AppPreferences{AIEnabled=true,AllowExternalAIProcessing=true,Model="test-model"});await s.Db.SaveChangesAsync();var secrets=new MemorySecrets();secrets.Write("openai","test-secret");var provider=new FakeAIProvider();var chat=new ChatService(s.Db,provider,new ReadTools(s.Db),secrets);var request=Guid.NewGuid();var events=new List<string>();await chat.Send(c.Id,"Find tasks",default,request,(name,data)=>{events.Add(name);return Task.CompletedTask;});await chat.Send(c.Id,"Find tasks",default,request);Assert.Equal(2,provider.Count);Assert.Equal(1,await s.Db.Messages.CountAsync(m=>m.Role=="user"));Assert.Equal("Completed",(await s.Db.ChatTurns.SingleAsync()).Status);Assert.Contains("ack",events);Assert.Contains("delta",events);Assert.Contains("tool",events);Assert.Contains("done",events);
    }
    private class StreamHandler(string body):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,"text/event-stream")});}
    [Fact]public async Task ProviderParsesRealSseFramingAndRejectsTruncatedStream()
    {
        var secrets=new MemorySecrets();secrets.Write("openai","test-secret");var frame="data: {\"type\":\"response.output_text.delta\",\"delta\":\"Hello\"}\n\n";var completed="data: {\"type\":\"response.completed\",\"response\":{\"id\":\"test\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"Hello\"}]}],\"usage\":{\"input_tokens\":1,\"output_tokens\":1}}}\n\n";
        using var http=new HttpClient(new StreamHandler(frame+completed));var received="";var response=await new OpenAIResponsesProvider(http,secrets).RespondStreaming("test",new(),new(),d=>{received+=d;return Task.CompletedTask;},default);Assert.Equal("Hello",received);Assert.Equal(received,response.Text);
        using var broken=new HttpClient(new StreamHandler(frame));await Assert.ThrowsAsync<ProviderUnavailableException>(()=>new OpenAIResponsesProvider(broken,secrets).RespondStreaming("test",new(),new(),_=>Task.CompletedTask,default));
    }
}
