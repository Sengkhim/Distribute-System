using DS.InventoryService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DS.InventoryService.Infrastructure.EFCoreDbContext;

public class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<Orders> Orders { get; set; }
    public DbSet<OrderItems> OrderItems { get; set; }
    public DbSet<Inventory> Inventory { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Inventory>().HasKey(m => m.Id);
        modelBuilder.Entity<Inventory>().Property(m => m.Id).HasMaxLength(36);
        modelBuilder.Entity<Inventory>().HasIndex(i => i.ProductCode).IsUnique();
        
        modelBuilder.Entity<Orders>().HasKey(m => m.Id);
        modelBuilder.Entity<Orders>().Property(m => m.Id).HasMaxLength(36);
        modelBuilder.Entity<Orders>().HasIndex(m => m.TransactionNo).IsUnique();
        modelBuilder.Entity<Orders>().HasIndex(i => i.Code).IsUnique();
        modelBuilder.Entity<Orders>().Property(i => i.Status).IsRequired();
        modelBuilder.Entity<Orders>().Property(i => i.Total).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Orders>().Property(i => i.Subtotal).HasColumnType("decimal(18,2)");

        
        modelBuilder.Entity<OrderItems>().HasKey(m => m.Id);
        modelBuilder.Entity<OrderItems>().Property(m => m.Id).HasMaxLength(36);
        modelBuilder.Entity<OrderItems>().HasIndex(i => i.Code).IsUnique();
        modelBuilder.Entity<OrderItems>().Property(m => m.ProductSnapshot).HasColumnType("jsonb");
        modelBuilder.Entity<OrderItems>().Property(i => i.Price).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<OrderItems>().Property(i => i.Subtotal).HasColumnType("decimal(18,2)");
        modelBuilder
            .Entity<OrderItems>()
            .HasOne(r => r.Orders)
            .WithMany(m => m.OrderItems)
            .HasForeignKey(f => f.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<OutboxMessage>().HasKey(m => m.Id);
        modelBuilder.Entity<OutboxMessage>().Property(m => m.Id).HasMaxLength(36);
        modelBuilder.Entity<OutboxMessage>().Property(m => m.Payload).HasColumnType("jsonb");
        modelBuilder.Entity<OutboxMessage>().Property(m => m.Type).HasMaxLength(255);
        modelBuilder.Entity<OutboxMessage>().Property(m => m.AggregateType).HasMaxLength(255);
        modelBuilder.Entity<OutboxMessage>().Property(m => m.AggregateId).HasMaxLength(255);
        modelBuilder.Entity<OutboxMessage>().HasIndex(m => m.Processed); // Index for efficient polling
    }
}