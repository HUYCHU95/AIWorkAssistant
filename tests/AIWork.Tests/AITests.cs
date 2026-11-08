using AIWork.Application;
using AIWork.Domain;
using AIWork.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;
namespace AIWork.Tests;
public class MemorySecrets:ISecretStore
{
    private readonly Dictionary<string,string> values=[];public string? Read(string name)=>values.GetValueOrDefault(name);public void Write(string name,string value)=>values[name]=value;
}
public class FakeAIProvider:IAIProvider
{
    public int Count;public bool Fail;public JsonArray? LastInput;
    public Task<ProviderResponse> Respond(string model,JsonArray input,JsonArray tools,CancellationToken ct)
    {
        Count++;LastInput=input.DeepClone().AsArray();if(Fail)throw new ProviderUnavailableException("test provider unavailable");
        if(Count==1)return Task.FromResult(new ProviderResponse("fake1","",[new("search_work_items","{\"query\":\"\",\"id\":null,\"limit\":5}","call1")],new JsonArray(new JsonObject{["type"]="function_call",["name"]="search_work_items",["arguments"]="{\"query\":\"\",\"id\":null,\"limit\":5}",["call_id"]="call1"}),100,10));
        return Task.FromResult(new ProviderResponse("fake2","TEST ONLY: grounded response",[],new JsonArray(new JsonObject{["type"]="message",["role"]="assistant",["content"]=new JsonArray(new JsonObject{["type"]="output_text",["text"]="TEST ONLY: grounded response"})}),120,20));
    }
}
public class AITests
{
    [Fact]public async Task OfflineConsentAndProviderFailureAreHonestAndPersistent()
    {
        using var s=new TestStore();var secrets=new MemorySecrets();secrets.Write("openai","test-only-secret");var fake=new FakeAIProvider();var conversation=new Conversation{Title="test"};s.Db.Conversations.Add(conversation);await s.Db.SaveChangesAsync();
        var chat=new ChatService(s.Db,fake,new ReadTools(s.Db),secrets);await chat.Send(conversation.Id,"Offline question",default);Assert.Equal(0,fake.Count);Assert.True(await s.Db.Messages.AnyAsync(m=>m.IsError));
        s.Db.Preferences.Add(new AppPreferences{AIEnabled=true,AllowExternalAIProcessing=true,Model="test-model",InputUsdPerMillion=1,OutputUsdPerMillion=2});await s.Db.SaveChangesAsync();await chat.Send(conversation.Id,"Find tasks",default);Assert.Equal(2,fake.Count);Assert.True(await s.Db.Messages.AnyAsync(m=>m.Role=="tool"));Assert.True(await s.Db.AIUsages.AnyAsync(u=>u.CostUsd!=null));
        Assert.Contains("function_call_output",fake.LastInput!.ToJsonString());Assert.DoesNotContain("test-only-secret",fake.LastInput.ToJsonString());
        fake.Fail=true;await chat.Send(conversation.Id,"Retry",default);Assert.True(await s.Db.Messages.AnyAsync(m=>m.IsError&&m.Content.Contains("test provider unavailable")));
        using var reopened=new WorkDb(s.Options);Assert.Equal(1,await reopened.Conversations.CountAsync());Assert.True(await reopened.Messages.CountAsync()>4);
    }
    [Fact]public async Task ReadToolsExcludeConfidentialSourcesAndWrongScope()
    {
        using var s=new TestStore();var p=new Project{Code="A",Name="A"};var other=new Project{Code="B",Name="B"};s.Db.Projects.AddRange(p,other);var a=new WorkTask{Title="allowed",ProjectId=p.Id};var b=new WorkTask{Title="excluded",ProjectId=other.Id};s.Db.Tasks.AddRange(a,b);
        var source=new TrackingService(s.Db).Capture(SourceKind.Email,"private","secret corporate text",null,null,true);await s.Db.SaveChangesAsync();var tools=new ReadTools(s.Db);var context=new Conversation{ProjectId=p.Id};
        var result=await tools.Run("search_work_items","{\"query\":\"\",\"id\":null,\"limit\":20}",context,Guid.NewGuid(),default);Assert.Contains("allowed",result);Assert.DoesNotContain("excluded",result);
        result=await tools.Run("search_knowledge","{\"query\":\"\",\"id\":null,\"limit\":20}",new Conversation(),Guid.NewGuid(),default);Assert.DoesNotContain("secret corporate text",result);
        result=await tools.Run("execute_sql","{}",context,Guid.NewGuid(),default);Assert.Contains("Rejected",result);
    }
    private class Handler:HttpMessageHandler
    {
        public string? Body;public string? Auth;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Body=await request.Content!.ReadAsStringAsync(ct);Auth=request.Headers.Authorization?.Parameter;Assert.Equal("https://api.openai.com/v1/responses",request.RequestUri!.ToString());return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("""{"id":"response-test","status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"real parser test"}]}],"usage":{"input_tokens":25,"output_tokens":6}}""",Encoding.UTF8,"application/json")};}
    }
    [Fact]public async Task ProductionProviderUsesResponsesWireFormatWithoutRemoteStorage()
    {
        var secrets=new MemorySecrets();secrets.Write("openai","test-secret");var h=new Handler();var provider=new OpenAIResponsesProvider(new HttpClient(h),secrets);var result=await provider.Respond("configured-model",new JsonArray(new JsonObject{["role"]="user",["content"]="synthetic test"}),new JsonArray(),default);
        Assert.Equal("real parser test",result.Text);Assert.Equal(25,result.InputTokens);Assert.Equal("test-secret",h.Auth);Assert.Contains("\"store\":false",h.Body);Assert.DoesNotContain("test-secret",h.Body);
    }
}
