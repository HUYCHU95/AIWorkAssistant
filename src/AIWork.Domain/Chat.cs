using System.ComponentModel.DataAnnotations;
namespace AIWork.Domain;
public class Conversation:Record
{
    [Required,StringLength(200)]public string Title{get;set;}="Cuộc trò chuyện mới";
    public Guid? ProjectId{get;set;}
    public Guid? TaskId{get;set;}
}
public class ChatMessage:Record
{
    public Guid ConversationId{get;set;}
    public string Role{get;set;}="user";
    public string Content{get;set;}="";
    public bool IsError{get;set;}
    public string? Model{get;set;}
    public string? ProviderId{get;set;}
    public string CorrelationId{get;set;}="";
}
public class AIUsage:Record
{
    public string Model{get;set;}="";
    public string CorrelationId{get;set;}="";
    public int? InputTokens{get;set;}
    public int? OutputTokens{get;set;}
    public decimal? CostUsd{get;set;}
    public string Outcome{get;set;}="";
}
public class AppPreferences
{
    public int Id{get;set;}=1;
    public bool AIEnabled{get;set;}
    public bool AllowExternalAIProcessing{get;set;}
    [StringLength(100)]public string Model{get;set;}="";
    [Range(1000,10000000)]public int MonthlyTokenLimit{get;set;}=100000;
    [Range(0,10000)]public decimal? InputUsdPerMillion{get;set;}
    [Range(0,10000)]public decimal? OutputUsdPerMillion{get;set;}
    public bool WorkerEnabled{get;set;}=true;
    [Range(0,23)]public int MorningHour{get;set;}=8;
    [Range(0,23)]public int EveningHour{get;set;}=17;
    public bool SmtpEnabled{get;set;}
    [StringLength(253)]public string? SmtpHost{get;set;}
    [Range(1,65535)]public int SmtpPort{get;set;}=587;
    public bool SmtpTls{get;set;}=true;
    [StringLength(200)]public string? SmtpUser{get;set;}
    [EmailAddress,StringLength(250)]public string? SmtpFrom{get;set;}
    [EmailAddress,StringLength(250)]public string? SmtpRecipient{get;set;}
    public string? SmtpVerifiedRecipient{get;set;}
    public bool SmtpAutomaticReminders{get;set;}
}
