using Microsoft.EntityFrameworkCore;
using EventPulse.PaymentService.Models;

namespace EventPulse.PaymentService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.BookingId)
                .IsRequired();

            entity.HasIndex(p => p.BookingId);

            entity.Property(p => p.BookingReference)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(p => p.CustomerId)
                .IsRequired();

            entity.Property(p => p.StripeSessionId)
                .HasMaxLength(255);

            entity.HasIndex(p => p.StripeSessionId);

            entity.Property(p => p.StripePaymentIntentId)
                .HasMaxLength(255);

            entity.Property(p => p.Amount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(p => p.Currency)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(p => p.Status)
                .IsRequired()
                .HasConversion<string>();

            entity.Property(p => p.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(o => o.Id);

            entity.Property(o => o.EventId)
                .IsRequired();

            entity.HasIndex(o => o.EventId)
                .IsUnique();

            entity.Property(o => o.EventType)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(o => o.Topic)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(o => o.MessageKey)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(o => o.Payload)
                .IsRequired();

            entity.Property(o => o.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(o => o.PublishedAtUtc);

            entity.HasIndex(o => new { o.PublishedAtUtc, o.CreatedAtUtc });
        });
    }
}
