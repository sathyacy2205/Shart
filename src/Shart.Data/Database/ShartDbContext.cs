using Microsoft.EntityFrameworkCore;
using Shart.Data.Entities;

namespace Shart.Data.Database;

/// <summary>
/// EF Core DbContext for the local SQLite database.
/// Stores transfer history, trusted devices, and user preferences.
/// </summary>
public sealed class ShartDbContext : DbContext
{
    public DbSet<TransferHistoryEntity> TransferHistory => Set<TransferHistoryEntity>();
    public DbSet<TrustedDeviceEntity> TrustedDevices => Set<TrustedDeviceEntity>();
    public DbSet<UserPreferenceEntity> UserPreferences => Set<UserPreferenceEntity>();

    public ShartDbContext(DbContextOptions<ShartDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransferHistoryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TransferredAtUtc);
            entity.HasIndex(e => e.DeviceId);
            entity.Property(e => e.FileName).HasMaxLength(500);
            entity.Property(e => e.DeviceName).HasMaxLength(200);
            entity.Property(e => e.Checksum).HasMaxLength(64);
        });

        modelBuilder.Entity<TrustedDeviceEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DeviceId).IsUnique();
            entity.Property(e => e.DeviceName).HasMaxLength(200);
        });

        modelBuilder.Entity<UserPreferenceEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Key).HasMaxLength(100);
            entity.Property(e => e.Value).HasMaxLength(2000);
        });
    }

    /// <summary>
    /// Ensure database is created and migrations applied.
    /// </summary>
    public static async Task InitializeAsync(ShartDbContext context)
    {
        await context.Database.EnsureCreatedAsync();
    }
}
