namespace AIWork.Domain;
public class Reminder:Record
{
    public Guid TaskId{get;set;}
    public DateTime ScheduledAtUtc{get;set;}
    public string Type{get;set;}="FollowUp";
    public string Status{get;set;}="Pending";
    public string DedupeKey{get;set;}="";
    public int RetryCount{get;set;}
    public DateTime? LastAttemptAtUtc{get;set;}
}
