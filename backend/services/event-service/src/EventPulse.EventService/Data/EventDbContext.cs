using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Models;

namespace EventPulse.EventService.Data;

public class EventDbContext : DbContext
{
    public EventDbContext(DbContextOptions<EventDbContext> options) : base(options)
    {
    }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<TicketType> TicketTypes => Set<TicketType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(e => e.Venue)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.EventDate)
                .IsRequired();

            entity.Property(e => e.Price)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(e => e.Status)
                .HasConversion<string>()
                .IsRequired();

            entity.Property(e => e.OrganizerId)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.ImageBlobName)
                .HasMaxLength(500)
                .IsRequired(false);

            entity.Property(e => e.CoverBlobName)
                .HasMaxLength(500)
                .IsRequired(false);

            entity.Property(e => e.ReviewComment)
                .HasMaxLength(1000)
                .IsRequired(false);

            entity.Property(e => e.Category)
                .HasMaxLength(100)
                .IsRequired(false);

            entity.Property(e => e.VenueType)
                .HasMaxLength(50)
                .IsRequired(false);
        });

                modelBuilder.Entity<TicketType>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(t => t.Price)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(t => t.Capacity)
                .IsRequired();

            entity.Property(t => t.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(t => t.Event)
                .WithMany(e => e.TicketTypes)
                .HasForeignKey(t => t.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.Property(t => t.BookedQuantity)
                .HasDefaultValue(0)
                .IsRequired();
        });
    }
}
