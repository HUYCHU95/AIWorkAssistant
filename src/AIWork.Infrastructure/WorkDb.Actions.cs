using AIWork.Domain;
using Microsoft.EntityFrameworkCore;
namespace AIWork.Infrastructure;
public partial class WorkDb
{
    public DbSet<ActionReceipt> Actions=>Set<ActionReceipt>();
    public DbSet<Reminder> Reminders=>Set<Reminder>();
    partial void ConfigureActions(ModelBuilder m)
    {
        m.Entity<Reminder>().HasIndex(r=>r.DedupeKey).IsUnique();m.Entity<Reminder>().Property(r=>r.Version).IsConcurrencyToken();m.Entity<Reminder>().HasOne<WorkTask>().WithMany().HasForeignKey(r=>r.TaskId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ActionReceipt>().HasIndex(a=>a.IdempotencyKey).IsUnique();m.Entity<ActionReceipt>().Property(a=>a.Version).IsConcurrencyToken();
        m.Entity<ActionReceipt>().HasOne<Conversation>().WithMany().HasForeignKey(a=>a.ConversationId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ActionReceipt>().HasOne<ChatMessage>().WithMany().HasForeignKey(a=>a.UserMessageId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ActionReceipt>().HasOne<SourceRecord>().WithMany().HasForeignKey(a=>a.OriginSourceId).OnDelete(DeleteBehavior.Restrict);
    }
}

