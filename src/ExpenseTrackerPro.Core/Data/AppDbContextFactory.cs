using Microsoft.EntityFrameworkCore;

namespace ExpenseTrackerPro.Core.Data;

/// <summary>
/// Creates a new short-lived <see cref="AppDbContext"/> for every call.
/// Callers are responsible for disposing the context.
/// </summary>
public sealed class AppDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public AppDbContextFactory(DbContextOptions<AppDbContext> options)
    {
        _options = options;
    }

    /// <summary>Creates a factory that stores data in the SQLite file at <paramref name="databasePath"/>.</summary>
    public static AppDbContextFactory ForFile(string databasePath)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new AppDbContextFactory(options);
    }

    public AppDbContext CreateDbContext()
    {
        return new AppDbContext(_options);
    }
}
