using Aygaz.ECommerce.Agent.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aygaz.ECommerce.Agent.Data;

public sealed class ECommerceDbContext(DbContextOptions<ECommerceDbContext> options)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

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
    }
}
