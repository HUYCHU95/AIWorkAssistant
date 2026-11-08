using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using AIWork.Infrastructure;
using AIWork.Domain;
using System.Text.Json;
using System.Text.Json.Nodes;

if(args.Length<2)throw new ArgumentException("snapshot|inspect|upgrade|ai-status|configure-ai|verify-ai <database> [snapshot destination]. configure-ai accepts JSON only on stdin; never pass a key as a command argument.");
var mode=args[0];var path=Path.GetFullPath(args[1]);if(!File.Exists(path))throw new FileNotFoundException("Database missing; refusing to create",path);
using var source=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Mode=mode is "upgrade" or "configure-ai"?SqliteOpenMode.ReadWrite:SqliteOpenMode.ReadOnly,Pooling=false,ForeignKeys=true,DefaultTimeout=10}.ToString());source.Open();
if(mode is "configure-ai" or "ai-status" or "verify-ai")
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
