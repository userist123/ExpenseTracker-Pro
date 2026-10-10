using ExpenseTrackerPro.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTrackerPro.Tests;

/// <summary>
/// An in-memory SQLite database that lives as long as this object: the connection is kept open,
/// because an in-memory database disappears when its last connection closes.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    public TestDatabase(bool initialize = true)
    {
        Connection = new SqliteConnection("Data Source=:memory:");
        Connection.Open();

        Options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(Connection).Options;
        Factory = new AppDbContextFactory(Options);

        if (initialize)
        {
            using var db = Factory.CreateDbContext();
            DatabaseInitializer.Initialize(db);
        }
    }

    public SqliteConnection Connection { get; }

    public DbContextOptions<AppDbContext> Options { get; }

    public AppDbContextFactory Factory { get; }

    public void Dispose()
    {
        Connection.Dispose();
    }
}
