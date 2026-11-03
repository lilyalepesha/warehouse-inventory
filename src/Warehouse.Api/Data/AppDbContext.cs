using Microsoft.EntityFrameworkCore;
using Warehouse.Api.Domain;

namespace Warehouse.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptItem> ReceiptItems => Set<ReceiptItem>();
    public DbSet<WriteOff> WriteOffs => Set<WriteOff>();
    public DbSet<WriteOffItem> WriteOffItems => Set<WriteOffItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Inn).HasMaxLength(12);
            entity.Property(x => x.ContactPerson).HasMaxLength(150);
            entity.Property(x => x.Phone).HasMaxLength(30);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(x => x.Sku).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.Sku).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100);
        });

        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.Property(x => x.Number).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.Number).IsUnique();
            entity.Property(x => x.InvoiceNumber).HasMaxLength(60);

            entity.HasOne(x => x.Supplier)
                .WithMany(x => x.Receipts)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Items)
                .WithOne(x => x.Receipt)
                .HasForeignKey(x => x.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReceiptItem>(entity =>
        {
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.RemainingQuantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);

            // SQLite хранит decimal как TEXT, поэтому CHECK-ограничение
            // сравнивало бы строки. Остаток контролируется в InventoryService.
            entity.HasOne(x => x.Product)
                .WithMany(x => x.ReceiptItems)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.ProductId, x.ExpiryDate });
        });

        modelBuilder.Entity<WriteOff>(entity =>
        {
            entity.Property(x => x.Number).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.Number).IsUnique();
            entity.Property(x => x.Comment).HasMaxLength(500);
            entity.HasIndex(x => new { x.OccurredOn, x.Reason });

            entity.HasMany(x => x.Items)
                .WithOne(x => x.WriteOff)
                .HasForeignKey(x => x.WriteOffId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WriteOffItem>(entity =>
        {
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.UnitSalePrice).HasPrecision(18, 2);
            entity.Ignore(x => x.Cost);
            entity.Ignore(x => x.Revenue);

            entity.HasOne(x => x.ReceiptItem)
                .WithMany(x => x.WriteOffItems)
                .HasForeignKey(x => x.ReceiptItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Product)
                .WithMany(x => x.WriteOffItems)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Receipt>().Ignore(x => x.TotalCost);
        modelBuilder.Entity<WriteOff>().Ignore(x => x.TotalCost);
        modelBuilder.Entity<WriteOff>().Ignore(x => x.TotalRevenue);
    }
}
