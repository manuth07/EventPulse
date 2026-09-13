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
    public DbSet<EventUpdateRequest> EventUpdateRequests => Set<EventUpdateRequest>();

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
        });

        modelBuilder.Entity<EventUpdateRequest>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.EventId)
                .IsRequired();

            entity.Property(r => r.OrganizerId)
                .IsRequired();

            entity.Property(r => r.Status)
                .HasConversion<string>()
                .IsRequired();

            entity.Property(r => r.RequestedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(r => r.ReviewedAt)
                .IsRequired(false);

            entity.Property(r => r.ReviewedBy)
                .IsRequired(false);

            entity.Property(r => r.ReviewComment)
                .HasMaxLength(1000)
                .IsRequired(false);

            entity.Property(r => r.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(r => r.Description)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(r => r.Venue)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(r => r.EventDate)
                .IsRequired();

            entity.Property(r => r.Category)
                .HasMaxLength(100)
                .IsRequired(false);

            entity.Property(r => r.VenueType)
                .HasMaxLength(50)
                .IsRequired(false);

            entity.Property(r => r.ImageBlobName)
                .HasMaxLength(500)
                .IsRequired(false);

            entity.Property(r => r.CoverBlobName)
                .HasMaxLength(500)
                .IsRequired(false);

            entity.HasOne(r => r.Event)
                .WithMany(e => e.UpdateRequests)
                .HasForeignKey(r => r.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            // Exactly ONE Pending update request per EventId (PostgreSQL partial unique index)
            entity.HasIndex(r => r.EventId)
                .IsUnique()
                .HasFilter("\"Status\" = 'Pending'");
        });
    }
}

