using System.Text.Json.Nodes;
namespace AIWork.Application;
public interface ISecretStore{string? Read(string name);void Write(string name,string value);}
public record ProviderCall(string Name,string Arguments,string CallId);
public record ProviderResponse(string? Id,string Text,IReadOnlyList<ProviderCall> Calls,JsonArray Output,int? InputTokens,int? OutputTokens);
public interface IAIProvider{Task<ProviderResponse> Respond(string model,JsonArray input,JsonArray tools,CancellationToken cancellationToken);}
public class ProviderUnavailableException(string message):Exception(message);
