using Microsoft.EntityFrameworkCore;
using EventPulse.BookingService.Models;

namespace EventPulse.BookingService.Data;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {
    }

    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingItem> BookingItems => Set<BookingItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.CustomerId)
                .IsRequired();

            entity.Property(c => c.EventId)
                .IsRequired();

            entity.Property(c => c.Status)
                .IsRequired();

            entity.Property(c => c.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(c => c.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(c => new { c.CustomerId, c.Status });

            entity.HasMany(c => c.Items)
                .WithOne(i => i.Cart)
                .HasForeignKey(i => i.CartId)
                .OnDelete(DeleteBehavior.Cascade);
        });

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

            // Exactly one row per (CartId, TicketTypeId)
            entity.HasIndex(c => new { c.CartId, c.TicketTypeId })
                .IsUnique();

            entity.HasIndex(c => c.CustomerId);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(b => b.Id);

            entity.Property(b => b.BookingReference)
                .IsRequired()
                .HasMaxLength(50);
            
            entity.HasIndex(b => b.BookingReference).IsUnique();

            entity.Property(b => b.TotalAmount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.HasMany(b => b.Items)
                .WithOne(i => i.Booking)
                .HasForeignKey(i => i.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BookingItem>(entity =>
        {
            entity.HasKey(i => i.Id);

            entity.Property(i => i.TicketName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(i => i.UnitPrice)
                .HasColumnType("decimal(18,2)")
                .IsRequired();
            
            entity.Property(i => i.Subtotal)
                .HasColumnType("decimal(18,2)")
                .IsRequired();
        });
    }
}