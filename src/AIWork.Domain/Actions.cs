using System.ComponentModel.DataAnnotations;
namespace AIWork.Domain;
public class ActionReceipt:Record
{
    public Guid? ConversationId{get;set;}
    public Guid? UserMessageId{get;set;}
    public Guid? OriginSourceId{get;set;}
    public string ToolName{get;set;}="";
    public string ArgumentsJson{get;set;}="";
    public string IdempotencyKey{get;set;}="";
    public string Status{get;set;}="PendingApproval";
    public string RiskLevel{get;set;}="A2";
    public string Summary{get;set;}="";
    public string? EntityType{get;set;}
    public Guid? EntityId{get;set;}
    public int? AppliedVersion{get;set;}
    public string? BeforeJson{get;set;}
    public string? AfterJson{get;set;}
    public string? ReviewedBy{get;set;}
    public DateTime? ReviewedAtUtc{get;set;}
    public DateTime? AppliedAtUtc{get;set;}
    public DateTime? UndoneAtUtc{get;set;}
}
