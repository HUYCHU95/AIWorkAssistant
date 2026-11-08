using System.ComponentModel.DataAnnotations;
namespace AIWork.Domain;
public enum TrackingKind { Issue, Commitment, Instruction, Decision }
public enum TrackingStatus { Open, InProgress, Waiting, Resolved, Verified, Cancelled }
public enum Verification { Reported, Verified, Conflicted, Superseded }
public enum SourceKind { Note, Email, Meeting, Instruction, Chat, File }
public class TrackingEntry : Record, IValidatableObject
{
    [EnumDataType(typeof(TrackingKind))] public TrackingKind Kind { get; set; }
    [Required,StringLength(300)] public string Title { get; set; }="";
    [Required,StringLength(12000)] public string Content { get; set; }="";
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? SourceId { get; set; }
    [StringLength(200)] public string? PersonText { get; set; }
    [EnumDataType(typeof(TrackingStatus))] public TrackingStatus Status { get; set; }
    [EnumDataType(typeof(Verification))] public Verification Verification { get; set; }
    [EnumDataType(typeof(Priority))] public Priority Severity { get; set; }=Priority.Normal;
    public DateOnly? TargetDate { get; set; }
    [EnumDataType(typeof(DeadlineType))] public DeadlineType? DueDateType { get; set; }
    [StringLength(4000)] public string? RootCause { get; set; }
    [StringLength(4000)] public string? NextAction { get; set; }
    public DateTime? NextCheckAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public Guid? SupersedesId { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if(TargetDate.HasValue!=DueDateType.HasValue)yield return new("Nhập đồng thời ngày và loại hạn.");
        if(Status==TrackingStatus.Verified && (Verification!=Verification.Verified||SourceId==null))yield return new("Xác minh cần nguồn bằng chứng và trạng thái Verified.");
    }
}
public class SourceRecord : Record
{
    [EnumDataType(typeof(SourceKind))] public SourceKind Kind { get; set; }
    [Required,StringLength(300)] public string Title { get; set; }="";
    [Required,StringLength(60000)] public string OriginalText { get; set; }="";
    public DateTime? OccurredAtUtc { get; set; }
    [StringLength(200)] public string? Sender { get; set; }
    public bool IsConfidential { get; set; }=true;
    public string OriginalSha256 { get; set; }="";
    public string CapturedBy { get; set; }="";
    public string Status { get; set; }="Captured";
}
public class SourceLink : Record
{
    public Guid SourceId { get; set; }
    public string EntityType { get; set; }="";
    public Guid EntityId { get; set; }
    public string LinkType { get; set; }="Reference";
}
public class TaskDependency : Record
{
    public Guid TaskId { get; set; }
    public Guid DependsOnTaskId { get; set; }
}
