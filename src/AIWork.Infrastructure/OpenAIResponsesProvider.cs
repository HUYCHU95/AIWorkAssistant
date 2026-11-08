using AIWork.Application;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace AIWork.Infrastructure;
public class OpenAIResponsesProvider(HttpClient http,ISecretStore secrets):IAIProvider
{
    public async Task<ProviderResponse> Respond(string model,JsonArray input,JsonArray tools,CancellationToken cancellationToken)
    {
        var key=secrets.Read("openai");if(string.IsNullOrWhiteSpace(key))throw new ProviderUnavailableException("Chưa có API key hợp lệ trong kho bí mật cục bộ.");
        using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.openai.com/v1/responses");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
        var body=new JsonObject{["model"]=model,["store"]=false,["max_output_tokens"]=1600,["parallel_tool_calls"]=false,["include"]=new JsonArray("reasoning.encrypted_content"),["input"]=input.DeepClone(),["tools"]=tools.DeepClone()};
        request.Content=new StringContent(body.ToJsonString(),Encoding.UTF8,"application/json");
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellationToken);
        if(!response.IsSuccessStatusCode)throw new ProviderUnavailableException($"OpenAI trả HTTP {(int)response.StatusCode}. Kiểm tra key/model/quota/kết nối; nội dung lỗi được giữ kín.");
        if(response.Content.Headers.ContentLength>1024*1024)throw new ProviderUnavailableException("Phản hồi vượt giới hạn.");
        await using var stream=await response.Content.ReadAsStreamAsync(cancellationToken);using var limited=new MemoryStream();var buffer=new byte[8192];int read;while((read=await stream.ReadAsync(buffer,cancellationToken))>0){if(limited.Length+read>1024*1024)throw new ProviderUnavailableException("Phản hồi vượt giới hạn.");limited.Write(buffer,0,read);}limited.Position=0;
        var root=await JsonNode.ParseAsync(limited,cancellationToken:cancellationToken)??throw new ProviderUnavailableException("Phản hồi rỗng.");
        var output=root["output"] as JsonArray??new JsonArray();var text=new StringBuilder();var calls=new List<ProviderCall>();
        foreach(var item in output){if(item?["type"]?.GetValue<string>()=="function_call")calls.Add(new(item["name"]!.GetValue<string>(),item["arguments"]!.GetValue<string>(),item["call_id"]!.GetValue<string>()));if(item?["type"]?.GetValue<string>()=="message")foreach(var part in item["content"]?.AsArray()??new JsonArray())if(part?["type"]?.GetValue<string>()=="output_text")text.AppendLine(part["text"]?.GetValue<string>());}
        if(root["status"]?.GetValue<string>()=="failed")throw new ProviderUnavailableException("Provider không hoàn tất yêu cầu.");
        return new(root["id"]?.GetValue<string>(),text.ToString().Trim(),calls,output.DeepClone().AsArray(),root["usage"]?["input_tokens"]?.GetValue<int>(),root["usage"]?["output_tokens"]?.GetValue<int>());
    }
}
