using ExpenseTrackerPro.Core.Data;
using Microsoft.Data.Sqlite;

namespace ExpenseTrackerPro.Core.Services;

/// <summary>
/// Backs up and restores the SQLite database file using SQLite's online backup API, which is safe while the
/// application keeps the database open and does not depend on file locks or journal files.
/// </summary>
public sealed class BackupService
{
    public const string BeforeRestoreFileName = "expense-tracker.before-restore.db";

    private const string TransactionsTable = "Transactions";

    private readonly string _databasePath;

    public BackupService(string databasePath)
    {
        _databasePath = Path.GetFullPath(databasePath);
    }

    /// <summary>Where the current data is saved just before a restore replaces it.</summary>
    public string BeforeRestorePath =>
        Path.Combine(Path.GetDirectoryName(_databasePath) ?? string.Empty, BeforeRestoreFileName);

    /// <summary>
    /// Writes a consistent copy of the database to <paramref name="destinationPath"/>, replacing an existing file.
    /// The copy is built next to the destination and moved into place, so a failure never damages an older backup.
    /// </summary>
    public OperationResult Backup(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return OperationResult.Fail("Alege un fișier pentru backup.");
        }

        if (IsLiveDatabase(destinationPath))
        {
            return OperationResult.Fail("Alege un alt fișier decât baza de date curentă.");
        }

        try
        {
            CopyToFile(_databasePath, Path.GetFullPath(destinationPath));
            return OperationResult.Ok();
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            return OperationResult.Fail($"Backup-ul nu a putut fi creat: {ex.Message}");
        }
    }

    /// <summary>
    /// Replaces the current data with the database in <paramref name="sourcePath"/>. The file is validated first;
    /// the current data is saved to <see cref="BeforeRestorePath"/>; an older backup is migrated to the current schema.
    /// If anything fails after the current data was touched, it is put back.
    /// </summary>
    public OperationResult Restore(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return OperationResult.Fail("Fișierul selectat nu există.");
        }

        if (IsLiveDatabase(sourcePath))
        {
            return OperationResult.Fail("Alege un alt fișier decât baza de date curentă.");
        }

        var source = Path.GetFullPath(sourcePath);
        var validation = Validate(source);
        if (validation is not null)
        {
            return OperationResult.Fail(validation);
        }

        var hadDatabase = File.Exists(_databasePath);

        try
        {
            SqliteConnection.ClearAllPools();

            if (hadDatabase)
            {
                CopyToFile(_databasePath, BeforeRestorePath);
            }

            try
            {
                Overwrite(_databasePath, source);
                MigrateCurrentDatabase();
            }
            catch
            {
                if (hadDatabase)
                {
                    SqliteConnection.ClearAllPools();
                    Overwrite(_databasePath, BeforeRestorePath);
                }

                throw;
            }

            return OperationResult.Ok();
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return OperationResult.Fail($"Restaurarea nu a putut fi făcută, datele curente au rămas neschimbate: {ex.Message}");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }
    }

    private bool IsLiveDatabase(string path)
    {
        return string.Equals(Path.GetFullPath(path), _databasePath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns a Romanian message when the file is not a SQLite database with a Transactions table.</summary>
    private static string? Validate(string path)
    {
        const string invalid = "Fișierul selectat nu este o copie de rezervă validă ExpenseTracker Pro.";

        try
        {
            using var connection = Open(path, SqliteOpenMode.ReadOnly);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
            command.Parameters.AddWithValue("$name", TransactionsTable);

            return Convert.ToInt64(command.ExecuteScalar()) > 0 ? null : invalid;
        }
        catch (SqliteException)
        {
            return invalid;
        }
    }

    /// <summary>Applies pending migrations to the live database (a no-op when the backup is already current).</summary>
    private void MigrateCurrentDatabase()
    {
        var factory = AppDbContextFactory.ForFile(_databasePath);
        using var db = factory.CreateDbContext();
        DatabaseInitializer.Initialize(db);
    }

    /// <summary>Copies <paramref name="from"/> to a new file, built next to the target and then moved into place.</summary>
    private static void CopyToFile(string from, string to)
    {
        var folder = Path.GetDirectoryName(to);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var temporary = to + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var source = Open(from, SqliteOpenMode.ReadOnly))
            using (var destination = Open(temporary, SqliteOpenMode.ReadWriteCreate))
            {
                source.BackupDatabase(destination);
            }

            File.Move(temporary, to, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <summary>
    /// Replaces the content of the database at <paramref name="target"/> with <paramref name="from"/> through the
    /// backup API, inside a single destination transaction: the file itself is never swapped or deleted.
    /// </summary>
    private static void Overwrite(string target, string from)
    {
        using var source = Open(from, SqliteOpenMode.ReadOnly);
        using var destination = Open(target, SqliteOpenMode.ReadWriteCreate);
        source.BackupDatabase(destination);
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Pooling = false,
        }.ToString());

        connection.Open();
        return connection;
    }
}
