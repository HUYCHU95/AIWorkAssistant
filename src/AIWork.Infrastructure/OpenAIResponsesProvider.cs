using AIWork.Application;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace AIWork.Infrastructure;
public class OpenAIResponsesProvider(HttpClient http,ISecretStore secrets):IAIProvider
{
    public Task<ProviderResponse> Respond(string model,JsonArray input,JsonArray tools,CancellationToken ct)=>Call(model,input,tools,null,ct);
    public Task<ProviderResponse> RespondStreaming(string model,JsonArray input,JsonArray tools,Func<string,Task> delta,CancellationToken ct)=>Call(model,input,tools,delta,ct);
    private async Task<ProviderResponse> Call(string model,JsonArray input,JsonArray tools,Func<string,Task>? delta,CancellationToken ct)
    {
        var key=secrets.Read("openai");if(string.IsNullOrWhiteSpace(key))throw new ProviderUnavailableException("Chưa có API key trong kho cục bộ.");
        using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.openai.com/v1/responses");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
        var body=new JsonObject{["model"]=model,["store"]=false,["max_output_tokens"]=6000,["parallel_tool_calls"]=false,["include"]=new JsonArray("reasoning.encrypted_content"),["input"]=input.DeepClone(),["tools"]=tools.DeepClone()};if(delta!=null)body["stream"]=true;
        request.Content=new StringContent(body.ToJsonString(),Encoding.UTF8,"application/json");using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        if(!response.IsSuccessStatusCode)throw new ProviderUnavailableException($"OpenAI trả HTTP {(int)response.StatusCode}. Kiểm tra key/model/quota/kết nối. Không có kết quả giả lập.");
        await using var stream=await response.Content.ReadAsStreamAsync(ct);
        if(delta==null)
        {
            using var limited=new MemoryStream();var buffer=new byte[8192];int read;while((read=await stream.ReadAsync(buffer,ct))>0){if(limited.Length+read>2*1024*1024)throw new ProviderUnavailableException("Phản hồi vượt giới hạn.");limited.Write(buffer,0,read);}limited.Position=0;
            return Parse(await JsonNode.ParseAsync(limited,cancellationToken:ct)??throw new ProviderUnavailableException("Phản hồi rỗng."));
        }
        using var reader=new StreamReader(stream);string? line;var data=new StringBuilder();var total=0;JsonNode? completed=null;
        async Task Consume()
        {
            if(data.Length==0)return;var json=data.ToString();data.Clear();if(json=="[DONE]")return;var item=JsonNode.Parse(json);var type=item?["type"]?.GetValue<string>();
            if(type=="response.output_text.delta")await delta(item?["delta"]?.GetValue<string>()??"");
            else if(type=="response.completed")completed=item?["response"]?.DeepClone();
            else if(type is "error" or "response.failed" or "response.incomplete")throw new ProviderUnavailableException("Provider không hoàn tất phản hồi. Có thể hết token/quota hoặc kết nối lỗi.");
        }
        while((line=await reader.ReadLineAsync(ct))!=null)
        {
            total+=line.Length;if(total>4*1024*1024)throw new ProviderUnavailableException("Stream vượt giới hạn.");if(line.Length==0){await Consume();continue;}if(line.StartsWith("data:",StringComparison.Ordinal)){if(data.Length>0)data.Append('\n');data.Append(line.AsSpan(5).TrimStart());}
        }
        await Consume();return completed!=null?Parse(completed):throw new ProviderUnavailableException("Stream kết thúc trước khi provider xác nhận hoàn tất.");
    }
    private static ProviderResponse Parse(JsonNode root)
    {
        if(root["status"]?.GetValue<string>() is "failed" or "incomplete")throw new ProviderUnavailableException("Provider không hoàn tất yêu cầu.");
        var output=root["output"] as JsonArray??new();var text=new StringBuilder();var calls=new List<ProviderCall>();
        foreach(var item in output){if(item?["type"]?.GetValue<string>()=="function_call")calls.Add(new(item["name"]!.GetValue<string>(),item["arguments"]!.GetValue<string>(),item["call_id"]!.GetValue<string>()));if(item?["type"]?.GetValue<string>()=="message")foreach(var part in item["content"]?.AsArray()??new())if(part?["type"]?.GetValue<string>()=="output_text")text.AppendLine(part["text"]?.GetValue<string>());}
        return new(root["id"]?.GetValue<string>(),text.ToString().Trim(),calls,output.DeepClone().AsArray(),root["usage"]?["input_tokens"]?.GetValue<int>(),root["usage"]?["output_tokens"]?.GetValue<int>());
    }
}
