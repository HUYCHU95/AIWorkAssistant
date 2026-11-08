using AIWork.Domain;
using Microsoft.EntityFrameworkCore;
namespace AIWork.Infrastructure;
public partial class WorkDb
{
    public DbSet<Notification> Notifications=>Set<Notification>();public DbSet<NotificationDelivery> Deliveries=>Set<NotificationDelivery>();public DbSet<WorkerHealth> WorkerHealth=>Set<WorkerHealth>();public DbSet<BackupManifest> BackupManifests=>Set<BackupManifest>();
    partial void ConfigurePlanner(ModelBuilder m)
    {
        m.Entity<Notification>().HasIndex(n=>n.DedupeKey).IsUnique();m.Entity<Notification>().Property(n=>n.Version).IsConcurrencyToken();m.Entity<Notification>().HasOne<WorkTask>().WithMany().HasForeignKey(n=>n.TaskId).OnDelete(DeleteBehavior.Restrict);m.Entity<Notification>().HasOne<Reminder>().WithMany().HasForeignKey(n=>n.ReminderId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<NotificationDelivery>().HasIndex(d=>d.DedupeKey).IsUnique();m.Entity<NotificationDelivery>().HasOne<Notification>().WithMany().HasForeignKey(d=>d.NotificationId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<WorkTask>().HasIndex(t=>t.NextCheckAtUtc);
    }
}
