using AIWork.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AIWork.Infrastructure;

public partial class WorkDb(DbContextOptions<WorkDb> options) : DbContext(options)
{
    public string Actor { get; set; } = "System";
    public DbSet<Workstream> Workstreams => Set<Workstream>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<WorkTask> Tasks => Set<WorkTask>();
    public DbSet<LocalUser> Users => Set<LocalUser>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<TrackingEntry> TrackingEntries => Set<TrackingEntry>();
    public DbSet<SourceRecord> Sources => Set<SourceRecord>();
    public DbSet<SourceLink> SourceLinks => Set<SourceLink>();
    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();
    protected override void OnModelCreating(ModelBuilder m)
    {
        ConfigureAI(m);
        ConfigureActions(m);
        ConfigureMemory(m);
        ConfigurePlanner(m);
        m.Entity<Workstream>().HasIndex(x => x.Code).IsUnique();
        m.Entity<Project>().HasIndex(x => x.Code).IsUnique();
        m.Entity<WorkTask>().HasIndex(x => x.ImportFingerprint).IsUnique();
        m.Entity<WorkTask>().HasIndex(x => new { x.Status, x.DueDate });
        m.Entity<Project>().HasIndex(x => x.Status);
        m.Entity<AuditEvent>().HasIndex(x => new { x.EntityId, x.OccurredAtUtc });
        m.Entity<Workstream>().HasOne<Workstream>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Project>().HasOne<Workstream>().WithMany().HasForeignKey(x => x.WorkstreamId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<WorkTask>().HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<WorkTask>().HasOne<Workstream>().WithMany().HasForeignKey(x => x.WorkstreamId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Workstream>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<Project>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<WorkTask>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<TrackingEntry>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<SourceRecord>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<TaskDependency>().HasIndex(x => new { x.TaskId, x.DependsOnTaskId }).IsUnique();
        m.Entity<SourceLink>().HasIndex(x => new { x.SourceId, x.EntityType, x.EntityId }).IsUnique();
        m.Entity<TrackingEntry>().HasIndex(x => new { x.Kind, x.Status, x.NextCheckAtUtc });
        m.Entity<TrackingEntry>().HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TrackingEntry>().HasOne<WorkTask>().WithMany().HasForeignKey(x=>x.TaskId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TrackingEntry>().HasOne<SourceRecord>().WithMany().HasForeignKey(x=>x.SourceId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TrackingEntry>().HasOne<TrackingEntry>().WithMany().HasForeignKey(x=>x.SupersedesId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SourceLink>().HasOne<SourceRecord>().WithMany().HasForeignKey(x=>x.SourceId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TaskDependency>().HasOne<WorkTask>().WithMany().HasForeignKey(x=>x.TaskId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<TaskDependency>().HasOne<WorkTask>().WithMany().HasForeignKey(x=>x.DependsOnTaskId).OnDelete(DeleteBehavior.Restrict);
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AuditChanges(); return base.SaveChanges(acceptAllChangesOnSuccess);
    }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AuditChanges(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
    private void AuditChanges()
    {
        ChangeTracker.DetectChanges();
        foreach(var source in ChangeTracker.Entries<SourceRecord>().Where(e=>e.State==EntityState.Modified))
            if(new[]{nameof(SourceRecord.OriginalText),nameof(SourceRecord.OriginalSha256),nameof(SourceRecord.OccurredAtUtc),nameof(SourceRecord.Sender),nameof(SourceRecord.Title),nameof(SourceRecord.Kind)}.Any(p=>source.Property(p).IsModified))
                throw new InvalidOperationException("Nguồn gốc là bất biến; hãy tạo nguồn/phiên bản mới.");
        foreach (var e in ChangeTracker.Entries<Record>().Where(x => x.State is EntityState.Added or EntityState.Modified).ToList())
        {
            var before = e.State == EntityState.Added ? null : JsonSerializer.Serialize(e.OriginalValues.Properties.ToDictionary(p => p.Name, p => e.OriginalValues[p]));
            e.Entity.UpdatedAtUtc = DateTime.UtcNow;
            e.Entity.Version++;
            AuditEvents.Add(new AuditEvent { Actor = Actor, Action = e.State == EntityState.Added ? "Create" : e.Entity.ArchivedAtUtc != null ? "Archive" : "Update", EntityType = e.Entity.GetType().Name, EntityId = e.Entity.Id.ToString(), BeforeJson = before, AfterJson = JsonSerializer.Serialize(e.Entity, e.Entity.GetType()) });
        }
    }
    public void Audit(string action, string entity, string id, string? details = null) => AuditEvents.Add(new AuditEvent { Actor = Actor, Action = action, EntityType = entity, EntityId = id, AfterJson = details });
    partial void ConfigureAI(ModelBuilder m);
    partial void ConfigureActions(ModelBuilder m);
    partial void ConfigureMemory(ModelBuilder m);
    partial void ConfigurePlanner(ModelBuilder m);
}
