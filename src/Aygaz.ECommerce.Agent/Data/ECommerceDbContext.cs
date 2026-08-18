using Aygaz.ECommerce.Agent.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aygaz.ECommerce.Agent.Data;

public sealed class ECommerceDbContext(DbContextOptions<ECommerceDbContext> options)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerOrder> CustomerOrders => Set<CustomerOrder>();

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
    }
}
