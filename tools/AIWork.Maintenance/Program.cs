using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using AIWork.Infrastructure;
using AIWork.Domain;
using System.Text.Json;
using System.Text.Json.Nodes;

if(args.Length<2)throw new ArgumentException("snapshot|inspect|upgrade|ai-status|configure-ai|verify-ai <database> [snapshot destination]. configure-ai accepts JSON only on stdin; never pass a key as a command argument.");
var mode=args[0];var path=Path.GetFullPath(args[1]);if(!File.Exists(path))throw new FileNotFoundException("Database missing; refusing to create",path);
using var source=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Mode=mode is "upgrade" or "configure-ai"?SqliteOpenMode.ReadWrite:SqliteOpenMode.ReadOnly,Pooling=false,ForeignKeys=true,DefaultTimeout=10}.ToString());source.Open();
if(mode is "configure-ai" or "ai-status" or "verify-ai" or "verify-experience")
{
    using var db=new WorkDb(new DbContextOptionsBuilder<WorkDb>().UseSqlite(source).Options){Actor="UserRequestedLocalSetup"};
    var secrets=new WindowsSecretStore(Path.Combine(Path.GetDirectoryName(path)!,"Secrets"));
    if(mode=="configure-ai")
    {
        var input=await Console.In.ReadToEndAsync();if(input.Length>5000)throw new ArgumentException("Configuration exceeds limit.");
        var request=JsonSerializer.Deserialize<LocalAIConfiguration>(input)??throw new ArgumentException("Invalid input.");
        if(string.IsNullOrWhiteSpace(request.Model)||request.Model.Length>100)throw new ArgumentException("A model ID is required.");
        var p=await db.Preferences.SingleOrDefaultAsync();if(p==null){p=new AppPreferences();db.Preferences.Add(p);}
        if(!string.IsNullOrWhiteSpace(request.ApiKey))secrets.Write("openai",request.ApiKey.Trim());
        p.Model=request.Model.Trim();p.AIEnabled=true;
        // Data-transfer permission is a separate, explicit setting; omitted input preserves it.
        if(request.AllowExternalAIProcessing.HasValue)p.AllowExternalAIProcessing=request.AllowExternalAIProcessing.Value;
        db.Audit("ConfigureAIFromConsole","Application","local",JsonSerializer.Serialize(new{p.Model,p.AIEnabled,p.AllowExternalAIProcessing,KeyConfigured=true,Secret="omitted"}));await db.SaveChangesAsync();
        Console.WriteLine("Local AI configuration saved; secret encrypted and omitted from output.");
    }
    var settings=await db.Preferences.AsNoTracking().SingleOrDefaultAsync();
    Console.WriteLine(JsonSerializer.Serialize(new{Model=settings?.Model,AIEnabled=settings?.AIEnabled??false,AllowExternalAIProcessing=settings?.AllowExternalAIProcessing??false,KeyConfigured=!string.IsNullOrWhiteSpace(secrets.Read("openai"))}));
    if(mode=="verify-experience")
    {
        if(settings==null||!settings.AIEnabled||!settings.AllowExternalAIProcessing)throw new InvalidOperationException("External AI not enabled.");
        using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(120)};var provider=new OpenAIResponsesProvider(http,secrets);var deltaCount=0;
        var streamed=await provider.RespondStreaming(settings.Model,new JsonArray(new JsonObject{["role"]="user",["content"]="Synthetic connection test. Reply exactly STREAM_OK. No business data."}),new(),delta=>{if(delta.Length>0)deltaCount++;return Task.CompletedTask;},default);
        Console.WriteLine(JsonSerializer.Serialize(new{Streaming=streamed.Text=="STREAM_OK"&&deltaCount>0?"VERIFIED":"CHECK",DeltaCount=deltaCount}));
        var folder=Path.Combine(Path.GetTempPath(),"AIWorkLiveSynthetic-"+Guid.NewGuid());Directory.CreateDirectory(folder);
        try
        {
            await using var test=new WorkDb(new DbContextOptionsBuilder<WorkDb>().UseSqlite($"Data Source={Path.Combine(folder,"synthetic.db")};Pooling=False;Foreign Keys=True").Options);await test.Database.MigrateAsync();test.Preferences.Add(new AppPreferences{AIEnabled=true,AllowExternalAIProcessing=true,Model=settings.Model});var tracking=new TrackingService(test);var original=tracking.Capture(SourceKind.File,"Synthetic public test only","Task: Review synthetic pump drawing. Owner: Linh. Deadline unknown. Do not claim complete.",null,null,false);await test.SaveChangesAsync();var attachments=new AttachmentService(test,Path.Combine(folder,"Attachments"));await attachments.Store(original.Id,AttachmentService.Check("synthetic-note.txt",System.Text.Encoding.UTF8.GetBytes("Task: Review synthetic pump drawing. Owner: Linh. Deadline unknown.")));
            if(args.Length>2)await attachments.Store(original.Id,AttachmentService.Check("synthetic-image.png",await File.ReadAllBytesAsync(args[2])));var draft=new AnalysisDraft{SourceId=original.Id,RequestKey=Guid.NewGuid().ToString()};test.AnalysisDrafts.Add(draft);await test.SaveChangesAsync();var content=new SourceContentService(test,attachments);var chat=new ChatService(test,provider,new ReadTools(test),secrets,content);await new AnalysisService(test,provider,chat,content,tracking,new ActionService(test,tracking)).Analyze(draft.Id,default);
            var result=draft.ResultJson==null?null:JsonSerializer.Deserialize<AnalysisResult>(draft.ResultJson);Console.WriteLine(JsonSerializer.Serialize(new{LiveFileAnalysis=draft.Status,Error=draft.ErrorCode,FieldCount=result?.Fields.Count,ItemCount=result?.Items.Count,NoBusinessWrites=await test.Tasks.CountAsync()==0}));if(draft.Status!="Ready"||result?.Items.Count==0)Environment.ExitCode=2;
        }
        finally{Directory.Delete(folder,true);}
    }
    if(mode=="verify-ai")
    {
        if(settings==null||string.IsNullOrWhiteSpace(settings.Model))throw new InvalidOperationException("No model configured.");
        // Explicit maintenance command sends ONLY this synthetic prompt, never DB business content.
        using var handler=new HttpClientHandler{AllowAutoRedirect=false};using var http=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(45)};
        try
        {
            var provider=new OpenAIResponsesProvider(http,secrets);var result=await provider.Respond(settings.Model,new JsonArray(new JsonObject{["role"]="user",["content"]="Connection test only. No business data. Reply with exactly OK."}),new JsonArray(),CancellationToken.None);
            Console.WriteLine(JsonSerializer.Serialize(new{LiveCall=result.Text.Trim()=="OK"?"VERIFIED":"RECEIVED_CHECK_OUTPUT",ResponseId=result.Id,InputTokens=result.InputTokens,OutputTokens=result.OutputTokens,HasText=!string.IsNullOrWhiteSpace(result.Text)}));
        }
        catch(AIWork.Application.ProviderUnavailableException ex){Console.WriteLine(ex.Message);Environment.ExitCode=2;}
        catch(Exception ex)when(ex is HttpRequestException or TaskCanceledException or JsonException){Console.WriteLine("Live test failed: "+ex.GetType().Name+"; no secret or response body logged.");Environment.ExitCode=2;}
    }
    return;
}
if(mode=="snapshot"){using var target=new SqliteConnection($"Data Source={Path.GetFullPath(args[2])};Pooling=False");target.Open();source.BackupDatabase(target);}
if(mode=="upgrade"){using var db=new WorkDb(new DbContextOptionsBuilder<WorkDb>().UseSqlite(source).Options);db.Database.Migrate();}
foreach(var table in new[]{"Projects","Tasks","AuditEvents","__EFMigrationsHistory"}){using var command=source.CreateCommand();command.CommandText=$"SELECT count(*) FROM \"{table}\"";try{Console.WriteLine($"{table}: {command.ExecuteScalar()}");}catch(SqliteException){Console.WriteLine($"{table}: unavailable");}}
using var check=source.CreateCommand();check.CommandText="PRAGMA quick_check";Console.WriteLine("quick_check: "+check.ExecuteScalar());
foreach(var table in new[]{"Projects","Tasks"}){using var command=source.CreateCommand();command.CommandText=$"SELECT Id FROM {table} ORDER BY Id";var ids=new System.Text.StringBuilder();using(var r=command.ExecuteReader())while(r.Read())ids.Append(r.GetString(0)).Append(';');Console.WriteLine(table+"IdSha256: "+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ids.ToString()))));}
public class LocalAIConfiguration
{
    public string? ApiKey{get;set;}
    public string Model{get;set;}="";
    public bool? AllowExternalAIProcessing{get;set;}
}
