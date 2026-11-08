using System.ComponentModel.DataAnnotations;

namespace AIWork.Domain;

public enum WorkstreamStatus { Active, Paused, Archived }
public enum ProjectPhase { Concept, Build, Order, Setup, Apply, Other }
public enum ProjectStatus { Planned, Active, Paused, Completed, Cancelled }
public enum Health { Unknown, OnTrack, AtRisk, Delayed, Blocked }
public enum WorkStatus { Todo, InProgress, Waiting, Blocked, Done, Cancelled }
public enum Priority { Low, Normal, High, Critical }
public enum DeadlineType { Committed, Planned, Suggested }

public abstract class Record
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAtUtc { get; set; }
    public int Version { get; set; }
}
public class Workstream : Record
{
    [Required, StringLength(40)] public string Code { get; set; } = "";
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [StringLength(4000)] public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    [EnumDataType(typeof(WorkstreamStatus))] public WorkstreamStatus Status { get; set; }
}
public class Project : Record
{
    [Required, StringLength(40)] public string Code { get; set; } = "";
    [Required, StringLength(300)] public string Name { get; set; } = "";
    [StringLength(4000)] public string? Objective { get; set; }
    [StringLength(4000)] public string? Scope { get; set; }
    public Guid? WorkstreamId { get; set; }
    [StringLength(200)] public string? OwnerText { get; set; }
    [EnumDataType(typeof(ProjectPhase))] public ProjectPhase Phase { get; set; }
    [EnumDataType(typeof(ProjectStatus))] public ProjectStatus Status { get; set; }
    [EnumDataType(typeof(Health))] public Health Health { get; set; }
    public DateOnly? BaselineTargetDate { get; set; }
    public DateOnly? CurrentTargetDate { get; set; }
    public DateOnly? ActualApplyDate { get; set; }
    [StringLength(4000)] public string? StatusReason { get; set; }
    [StringLength(4000)] public string? SourceLabel { get; set; }
}
public class WorkTask : Record, IValidatableObject
{
    [Required, StringLength(500)] public string Title { get; set; } = "";
    [StringLength(8000)] public string? Description { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? WorkstreamId { get; set; }
    [StringLength(200)] public string? OwnerText { get; set; }
    [EnumDataType(typeof(WorkStatus))] public WorkStatus Status { get; set; }
    [EnumDataType(typeof(Priority))] public Priority Priority { get; set; } = Priority.Normal;
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    [EnumDataType(typeof(DeadlineType))] public DeadlineType? DueDateType { get; set; }
    [StringLength(4000)] public string? NextAction { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    [StringLength(4000)] public string? CompletionEvidence { get; set; }
    [StringLength(4000)] public string? SourceLabel { get; set; }
    public string? ImportFingerprint { get; set; }
    [StringLength(200)] public string? FollowUpPersonText { get; set; }
    [StringLength(1000)] public string? WaitingFor { get; set; }
    [StringLength(1000)] public string? Blocker { get; set; }
    public DateTime? NextCheckAtUtc { get; set; }
    public DateTime? LastAskedAtUtc { get; set; }
    public int? LastAskedVersion { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (DueDate.HasValue != DueDateType.HasValue) yield return new("Ngày hạn và loại hạn phải được nhập cùng nhau.", [nameof(DueDateType)]);
        if (StartDate > DueDate) yield return new("Ngày bắt đầu phải trước hoặc bằng hạn.", [nameof(StartDate)]);
        if (ProjectId != null && WorkstreamId != null) yield return new("Công việc dự án sử dụng luồng công việc của dự án.", [nameof(WorkstreamId)]);
    }
    public bool IsOverdue(DateOnly today) => ArchivedAtUtc == null && DueDate < today && Status is not (WorkStatus.Done or WorkStatus.Cancelled);
}
public class LocalUser
{
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public int Id { get; set; } = 1;
    public string UserName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
}
public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string Actor { get; set; } = "System";
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}
public class ImportBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourceFileName { get; set; } = "";
    public string FileSha256 { get; set; } = "";
    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;
    public int CreatedCount { get; set; }
    public int SkippedCount { get; set; }
    public string WarningsJson { get; set; } = "";
    public string MappingVersion { get; set; } = "9projects-v1";
}
