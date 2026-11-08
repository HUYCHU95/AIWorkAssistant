using System.ComponentModel.DataAnnotations;
namespace AIWork.Domain;
public class ExperienceSettings
{
    public int Id{get;set;}=1;
    [RegularExpression("^(Light|Dark|System)$")]public string Theme{get;set;}="System";
    [RegularExpression("^(Enter|CtrlEnter)$")]public string SendKey{get;set;}="Enter";
    [RegularExpression("^#[0-9a-fA-F]{6}$")]public string HomeAccent{get;set;}="#3665e8";
    [RegularExpression("^#[0-9a-fA-F]{6}$")]public string AIAccent{get;set;}="#7c4dde";
    [RegularExpression("^#[0-9a-fA-F]{6}$")]public string WorkAccent{get;set;}="#087f8c";
    [RegularExpression("^#[0-9a-fA-F]{6}$")]public string PlanningAccent{get;set;}="#a85b13";
    [RegularExpression("^#[0-9a-fA-F]{6}$")]public string SettingsAccent{get;set;}="#526277";
    public bool UnifiedAccent{get;set;}
    public string PageAccentsJson{get;set;}="{}";
}
public class AnalysisDraft:Record
{
    public string RequestKey{get;set;}="";
    public Guid SourceId{get;set;}
    public string TargetKind{get;set;}="Task";
    public string Status{get;set;}="Captured";
    public string? ResultJson{get;set;}
    public string? ErrorCode{get;set;}
}
public class ChatTurn:Record
{
    public Guid RequestId{get;set;}
    public Guid ConversationId{get;set;}
    public Guid? SourceId{get;set;}
    public Guid UserMessageId{get;set;}
    public string Status{get;set;}="Running";
}
