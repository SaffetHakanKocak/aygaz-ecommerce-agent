using Aygaz.ECommerce.Agent.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aygaz.ECommerce.Agent.Data;

public sealed class ECommerceDbContext(DbContextOptions<ECommerceDbContext> options)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerOrder> CustomerOrders => Set<CustomerOrder>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<InventoryRecord> InventoryRecords => Set<InventoryRecord>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(customer =>
        {
            customer.ToTable("Customers");

            customer.HasKey(entity => entity.Id);

            customer.Property(entity => entity.FirstName)
                .HasMaxLength(100)
                .IsRequired();

            customer.Property(entity => entity.LastName)
                .HasMaxLength(100)
                .IsRequired();

            customer.Property(entity => entity.Email)
                .HasMaxLength(254)
                .UseCollation("NOCASE")
                .IsRequired();

            customer.HasIndex(entity => entity.Email)
                .IsUnique();

            customer.Property(entity => entity.Phone)
                .HasMaxLength(30);

            customer.Property(entity => entity.City)
                .HasMaxLength(100);

            customer.Property(entity => entity.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<CustomerOrder>(order =>
        {
            order.ToTable(
                "CustomerOrders",
                table => table.HasCheckConstraint(
                    "CK_CustomerOrders_TotalAmount_NonNegative",
                    "\"TotalAmount\" >= 0"));

            order.HasKey(entity => entity.Id);

            order.Property(entity => entity.OrderNumber)
                .HasMaxLength(CustomerOrder.MaximumOrderNumberLength)
                .UseCollation("NOCASE")
                .IsRequired();

            order.HasIndex(entity => entity.OrderNumber)
                .IsUnique();

            order.Property(entity => entity.OrderDate)
                .IsRequired();

            order.Property(entity => entity.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            order.Property(entity => entity.TotalAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            order.HasIndex(entity => entity.CustomerId);

            order.HasOne(entity => entity.Customer)
                .WithMany(customer => customer.Orders)
                .HasForeignKey(entity => entity.CustomerId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Product>(product =>
        {
            product.ToTable(
                "Products",
                table => table.HasCheckConstraint(
                    "CK_Products_UnitPrice_NonNegative",
                    "\"UnitPrice\" >= 0"));

            product.HasKey(entity => entity.Id);

            product.Property(entity => entity.Sku)
                .HasMaxLength(Product.MaximumSkuLength)
                .UseCollation("NOCASE")
                .IsRequired();

            product.HasIndex(entity => entity.Sku)
                .IsUnique();

            product.Property(entity => entity.Name)
                .HasMaxLength(Product.MaximumNameLength)
                .UseCollation("NOCASE")
                .IsRequired();

            product.HasIndex(entity => entity.Name);

            product.Property(entity => entity.Category)
                .HasMaxLength(Product.MaximumCategoryLength)
                .UseCollation("NOCASE")
                .IsRequired();

            product.HasIndex(entity => entity.Category);

            product.Property(entity => entity.UnitPrice)
                .HasPrecision(18, 2)
                .IsRequired();

            product.Property(entity => entity.IsActive)
                .IsRequired();

            product.Property(entity => entity.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<InventoryRecord>(inventory =>
        {
            inventory.ToTable(
                "InventoryRecords",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_InventoryRecords_QuantityAvailable_NonNegative",
                        "\"QuantityAvailable\" >= 0");
                    table.HasCheckConstraint(
                        "CK_InventoryRecords_ReorderLevel_NonNegative",
                        "\"ReorderLevel\" >= 0");
                });

            inventory.HasKey(entity => entity.Id);

            inventory.Property(entity => entity.LocationCode)
                .HasMaxLength(InventoryRecord.MaximumLocationCodeLength)
                .UseCollation("NOCASE")
                .IsRequired();

            inventory.Property(entity => entity.LocationName)
                .HasMaxLength(InventoryRecord.MaximumLocationNameLength)
                .IsRequired();

            inventory.Property(entity => entity.QuantityAvailable)
                .IsRequired();

            inventory.Property(entity => entity.ReorderLevel)
                .IsRequired();

            inventory.Property(entity => entity.UpdatedAt)
                .IsRequired();

            inventory.HasIndex(entity => new
                {
                    entity.ProductId,
                    entity.LocationCode
                })
                .IsUnique();

            inventory.HasOne(entity => entity.Product)
                .WithMany(product => product.InventoryRecords)
                .HasForeignKey(entity => entity.ProductId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderItem>(orderItem =>
        {
            orderItem.ToTable(
                "OrderItems",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_OrderItems_Quantity_Positive",
                        "\"Quantity\" > 0");
                    table.HasCheckConstraint(
                        "CK_OrderItems_UnitPrice_NonNegative",
                        "\"UnitPrice\" >= 0");
                });

            orderItem.HasKey(entity => entity.Id);

            orderItem.Property(entity => entity.Quantity)
                .IsRequired();

            orderItem.Property(entity => entity.UnitPrice)
                .HasPrecision(18, 2)
                .IsRequired();

            orderItem.HasIndex(entity => new
                {
                    entity.CustomerOrderId,
                    entity.ProductId
                })
                .IsUnique();

            orderItem.HasIndex(entity => entity.ProductId);

            orderItem.HasOne(entity => entity.CustomerOrder)
                .WithMany(order => order.OrderItems)
                .HasForeignKey(entity => entity.CustomerOrderId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            orderItem.HasOne(entity => entity.Product)
                .WithMany(product => product.OrderItems)
                .HasForeignKey(entity => entity.ProductId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
