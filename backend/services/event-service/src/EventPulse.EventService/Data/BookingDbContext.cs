using Microsoft.EntityFrameworkCore;
using EventPulse.BookingService.Models;

namespace EventPulse.BookingService.Data;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {
    }

    public DbSet<CartItem> CartItems => Set<CartItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.TicketTypeName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(c => c.UnitPrice)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(c => c.Quantity)
                .IsRequired();

            entity.Property(c => c.AddedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // One row per customer/ticket-type combination
            entity.HasIndex(c => new { c.CustomerId, c.TicketTypeId })
                .IsUnique();
        });
    }
}