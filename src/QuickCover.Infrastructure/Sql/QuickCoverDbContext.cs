using MassTransit;
using Microsoft.EntityFrameworkCore;
using QuickCover.Domain.Policies;

namespace QuickCover.Infrastructure.Sql;

/// <summary>Azure SQL in the cloud, SQL Server in Docker locally.</summary>
public sealed class QuickCoverDbContext(DbContextOptions<QuickCoverDbContext> options) : DbContext(options)
{
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<Policy> Policies => Set<Policy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Purchase>(e =>
        {
            e.ToTable("Purchases");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.QuoteId).IsUnique(); // the real "can't buy twice" guarantee
            e.Property(x => x.CustomerId).HasMaxLength(100).IsRequired();
            e.Property(x => x.PaymentReference).HasMaxLength(50).IsRequired();
            e.Property(x => x.AmountPaid).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Policy>(e =>
        {
            e.ToTable("Policies");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.QuoteId).IsUnique(); // idempotency backstop for the consumer
            e.HasIndex(x => x.PolicyNumber).IsUnique();
            e.Property(x => x.PolicyNumber).HasMaxLength(30).IsRequired();
            e.Property(x => x.CustomerId).HasMaxLength(100).IsRequired();
            e.Property(x => x.AnnualPremium).HasPrecision(10, 2);
        });

        // MassTransit transactional outbox + inbox tables.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
