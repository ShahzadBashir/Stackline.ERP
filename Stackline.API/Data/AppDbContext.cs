using Microsoft.EntityFrameworkCore;
using Stackline.API.Data.Entities;

namespace Stackline.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Item> Items => Set<Item>();
        public DbSet<Warehouse> Warehouses => Set<Warehouse>();
        public DbSet<StockLevel> StockLevels => Set<StockLevel>();
        public DbSet<StockMovement> StockMovements => Set<StockMovement>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Customer> Customers => Set<Customer>();

        public DbSet<Purchase> Purchases => Set<Purchase>();

        public DbSet<PurchaseLine> PurchaseLines => Set<PurchaseLine>();

        public DbSet<Sale> Sales => Set<Sale>();

        public DbSet<SaleLine> SaleLines => Set<SaleLine>();

        public DbSet<CustomerReceipt> CustomerReceipts =>
        Set<CustomerReceipt>();

        public DbSet<SupplierPayment> SupplierPayments =>
        Set<SupplierPayment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Item>().HasIndex(i => i.SKU).IsUnique();

            modelBuilder.Entity<StockLevel>()
                .HasIndex(s => new { s.ItemId, s.WarehouseId }).IsUnique();

            modelBuilder.Entity<Category>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Item>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Warehouse>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<StockLevel>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<StockMovement>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Supplier>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Customer>().HasQueryFilter(e => !e.IsDeleted);

            modelBuilder.Entity<Purchase>(entity =>
            {
                entity.HasQueryFilter(p => !p.IsDeleted);

                entity.Property(p => p.PurchaseNumber)
                    .HasMaxLength(50);

                entity.HasIndex(p => p.PurchaseNumber)
                    .IsUnique();

                entity.Property(p => p.SupplierInvoiceNumber)
                    .HasMaxLength(100);

                entity.Property(p => p.Notes)
                    .HasMaxLength(2000);

                entity.Property(p => p.Status)
                    .HasMaxLength(20);

                entity.Property(p => p.TotalAmount)
                    .HasPrecision(18, 2);

                entity.HasOne<Supplier>()
                    .WithMany()
                    .HasForeignKey(p => p.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Warehouse>()
                    .WithMany()
                    .HasForeignKey(p => p.WarehouseId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(p => p.Lines)
                    .WithOne(l => l.Purchase)
                    .HasForeignKey(l => l.PurchaseId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PurchaseLine>(entity =>
            {
                entity.Property(l => l.Quantity)
                    .HasPrecision(18, 3);

                entity.Property(l => l.UnitCost)
                    .HasPrecision(18, 4);

                entity.Property(l => l.LineTotal)
                    .HasPrecision(18, 2);

                entity.HasIndex(l => new { l.PurchaseId, l.ItemId })
                    .IsUnique();

                entity.HasOne<Item>()
                    .WithMany()
                    .HasForeignKey(l => l.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Sale>(entity =>
            {
                entity.HasQueryFilter(s => !s.IsDeleted);

                entity.Property(s => s.SaleNumber)
                    .HasMaxLength(50);

                entity.HasIndex(s => s.SaleNumber)
                    .IsUnique();

                entity.Property(s => s.Status)
                    .HasMaxLength(20);

                entity.Property(s => s.Notes)
                    .HasMaxLength(2000);

                entity.Property(s => s.TotalAmount)
                    .HasPrecision(18, 2);

                entity.Property(s => s.AmountPaid)
                    .HasPrecision(18, 2);

                entity.HasOne<Customer>()
                    .WithMany()
                    .HasForeignKey(s => s.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Warehouse>()
                    .WithMany()
                    .HasForeignKey(s => s.WarehouseId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(s => s.Lines)
                    .WithOne(l => l.Sale)
                    .HasForeignKey(l => l.SaleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SaleLine>(entity =>
            {
                entity.Property(l => l.Quantity)
                    .HasPrecision(18, 3);

                entity.Property(l => l.UnitPrice)
                    .HasPrecision(18, 4);

                entity.Property(l => l.LineTotal)
                    .HasPrecision(18, 2);

                entity.HasIndex(l => new { l.SaleId, l.ItemId })
                    .IsUnique();

                entity.HasOne<Item>()
                    .WithMany()
                    .HasForeignKey(l => l.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CustomerReceipt>(entity =>
            {
                entity.HasQueryFilter(r => !r.IsDeleted);

                entity.Property(r => r.ReceiptNumber)
                    .HasMaxLength(50);

                entity.HasIndex(r => r.ReceiptNumber)
                    .IsUnique();

                entity.Property(r => r.Status)
                    .HasMaxLength(20);

                entity.Property(r => r.PaymentMethod)
                    .HasMaxLength(30);

                entity.Property(r => r.PaymentReference)
                    .HasMaxLength(100);

                entity.Property(r => r.Notes)
                    .HasMaxLength(2000);

                entity.Property(r => r.Amount)
                    .HasPrecision(18, 2);

                entity.HasIndex(r => new
                {
                    r.CustomerId,
                    r.Status,
                    r.ReceiptDate
                });

                entity.HasOne<Customer>()
                    .WithMany()
                    .HasForeignKey(r => r.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SupplierPayment>(entity =>
            {
                entity.HasQueryFilter(payment => !payment.IsDeleted);

                entity.Property(payment => payment.PaymentNumber)
                    .HasMaxLength(50);

                entity.HasIndex(payment => payment.PaymentNumber)
                    .IsUnique();

                entity.Property(payment => payment.Status)
                    .HasMaxLength(20);

                entity.Property(payment => payment.PaymentMethod)
                    .HasMaxLength(30);

                entity.Property(payment => payment.PaymentReference)
                    .HasMaxLength(100);

                entity.Property(payment => payment.Notes)
                    .HasMaxLength(2000);

                entity.Property(payment => payment.Amount)
                    .HasPrecision(18, 2);

                entity.HasIndex(payment => new
                {
                    payment.SupplierId,
                    payment.Status,
                    payment.PaymentDate
                });

                entity.HasOne<Supplier>()
                    .WithMany()
                    .HasForeignKey(payment => payment.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
