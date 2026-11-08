using AIWork.Domain;
using System.Text.Json.Nodes;
namespace AIWork.Application;
public interface IChatTools
{
    JsonArray Definitions{get;}
    Task<string> Run(string name,string arguments,Conversation context,Guid userMessageId,CancellationToken cancellationToken);
}
