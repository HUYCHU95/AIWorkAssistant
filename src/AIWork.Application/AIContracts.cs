using System.Text.Json.Nodes;
namespace AIWork.Application;
public interface ISecretStore{string? Read(string name);void Write(string name,string value);}
public record ProviderCall(string Name,string Arguments,string CallId);
public record ProviderResponse(string? Id,string Text,IReadOnlyList<ProviderCall> Calls,JsonArray Output,int? InputTokens,int? OutputTokens);
public interface IAIProvider
{
    Task<ProviderResponse> Respond(string model,JsonArray input,JsonArray tools,CancellationToken cancellationToken);
    async Task<ProviderResponse> RespondStreaming(string model,JsonArray input,JsonArray tools,Func<string,Task> onDelta,CancellationToken cancellationToken)
    {
        var result=await Respond(model,input,tools,cancellationToken);if(result.Text.Length>0)await onDelta(result.Text);return result;
    }
}
public class ProviderUnavailableException(string message):Exception(message);
