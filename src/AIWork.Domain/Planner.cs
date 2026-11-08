namespace AIWork.Domain;
public class Notification:Record
{
    public string DedupeKey{get;set;}="";
    public string Title{get;set;}="";
    public string Body{get;set;}="";
    public Guid? TaskId{get;set;}
    public Guid? ReminderId{get;set;}
    public DateTime? ReadAtUtc{get;set;}
    public DateTime? DismissedAtUtc{get;set;}
    public DateTime? SnoozedUntilUtc{get;set;}
}
public class NotificationDelivery:Record
{
    public Guid? NotificationId{get;set;}
    public string DedupeKey{get;set;}="";
    public string Channel{get;set;}="SMTP";
    public string Result{get;set;}="Sending";
    public string? ErrorCode{get;set;}
}
public class WorkerHealth
{
    public int Id{get;set;}=1;
    public DateTime? LastRunAtUtc{get;set;}
    public string? LastErrorCode{get;set;}
}
public class BackupManifest:Record
{
    public string FileName{get;set;}="";
    public string Sha256{get;set;}="";
    public string SchemaVersion{get;set;}="";
    public string ValidationStatus{get;set;}="Verified";
}
