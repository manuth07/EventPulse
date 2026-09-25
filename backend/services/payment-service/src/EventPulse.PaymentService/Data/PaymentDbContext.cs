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
            entity.HasKey(payment => payment.Id);

            entity.Property(payment => payment.BookingId)
                .IsRequired();

            entity.HasIndex(payment => payment.BookingId);

            entity.Property(payment => payment.BookingReference)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(payment => payment.CustomerId)
                .IsRequired();

            entity.Property(payment => payment.StripeSessionId)
                .HasMaxLength(255);

            entity.HasIndex(payment => payment.StripeSessionId);

            entity.Property(payment => payment.StripePaymentIntentId)
                .HasMaxLength(255);

            entity.Property(payment => payment.Amount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(payment => payment.Currency)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(payment => payment.Status)
                .IsRequired()
                .HasConversion<string>();

            entity.Property(payment => payment.CreatedAt)
                .IsRequired();
        });
    }
}