using ExpenseTrackerPro.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTrackerPro.Core.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<TransactionItem> Transactions => Set<TransactionItem>();

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var transaction = modelBuilder.Entity<TransactionItem>();
        transaction.HasKey(x => x.Id);
        transaction.Property(x => x.Type)
            .HasConversion(new TransactionTypeConverter())
            .HasMaxLength(20)
            .IsRequired();
        transaction.Property(x => x.Category).HasMaxLength(60).IsRequired();
        transaction.Property(x => x.Description).HasMaxLength(120).IsRequired();
        transaction.Property(x => x.Amount).HasPrecision(18, 2);
        transaction.HasIndex(x => x.Date);

        var setting = modelBuilder.Entity<AppSetting>();
        setting.ToTable("AppSettings");
        setting.HasKey(x => x.Key);
        setting.Property(x => x.Key).HasMaxLength(60).IsRequired();
        setting.Property(x => x.Value).HasMaxLength(200).IsRequired();
    }
}
