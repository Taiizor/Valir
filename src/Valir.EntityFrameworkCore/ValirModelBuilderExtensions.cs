using Microsoft.EntityFrameworkCore;

namespace Valir.EntityFrameworkCore;

/// <summary>
/// Extension methods for configuring EF Core model with Valir outbox.
/// </summary>
public static class ValirModelBuilderExtensions
{
    /// <summary>
    /// Configure the Valir outbox table.
    /// Call this in your DbContext.OnModelCreating.
    /// </summary>
    public static ModelBuilder ConfigureValirOutbox(this ModelBuilder modelBuilder, string tableName = "ValirOutbox")
    {
        modelBuilder.Entity<OutboxJob>(entity =>
        {
            entity.ToTable(tableName);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.JobId)
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(e => e.Type)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(e => e.PayloadBase64)
                .IsRequired();

            entity.Property(e => e.IdempotencyKey)
                .HasMaxLength(256);

            entity.Property(e => e.LastError)
                .HasMaxLength(2000);

            // Index for efficient polling
            entity.HasIndex(e => new { e.IsProcessed, e.CreatedAt });

            // Unique index on job ID
            entity.HasIndex(e => e.JobId)
                .IsUnique();
        });

        return modelBuilder;
    }
}
