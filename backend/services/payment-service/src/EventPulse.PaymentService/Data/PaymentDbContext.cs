using Microsoft.EntityFrameworkCore;
using EventPulse.PaymentService.Models;

namespace EventPulse.PaymentService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<Payment> Payments => Set<Payment>();

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
    }
}
