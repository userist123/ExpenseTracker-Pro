using System.Data;
using System.Data.Common;
using System.Reflection;
using ExpenseTrackerPro.Core.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ExpenseTrackerPro.Core.Data;

public static class DatabaseInitializer
{
    private const string TransactionsTable = "Transactions";
    private const string HistoryTable = "__EFMigrationsHistory";

    /// <summary>
    /// Brings the database to the latest schema.
    /// Databases created by the old <c>EnsureCreated()</c> code have the <c>Transactions</c> table but no
    /// migrations history; they are first baselined to <c>InitialCreate</c> (whose schema is identical),
    /// then the remaining migrations are applied normally.
    /// </summary>
    public static void Initialize(AppDbContext db)
    {
        BaselineLegacyDatabase(db);
        db.Database.Migrate();
    }

    private static void BaselineLegacyDatabase(AppDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            if (!TableExists(connection, TransactionsTable) || TableExists(connection, HistoryTable))
            {
                return;
            }

            Execute(
                connection,
                $"CREATE TABLE \"{HistoryTable}\" (" +
                "\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, " +
                "\"ProductVersion\" TEXT NOT NULL);");

            Execute(
                connection,
                $"INSERT INTO \"{HistoryTable}\" (\"MigrationId\", \"ProductVersion\") " +
                $"VALUES ('{GetInitialMigrationId()}', '{GetEfProductVersion()}');");
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    private static string GetInitialMigrationId()
    {
        var attribute = typeof(InitialCreate).GetCustomAttribute<MigrationAttribute>()
            ?? throw new InvalidOperationException("Migrarea InitialCreate nu are un identificator.");

        return attribute.Id;
    }

    private static string GetEfProductVersion()
    {
        var informational = typeof(DbContext).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // "8.0.31+<commit hash>" -> "8.0.31"
        var version = informational?.Split('+')[0];

        return string.IsNullOrWhiteSpace(version)
            ? typeof(DbContext).Assembly.GetName().Version?.ToString(3) ?? "8.0.0"
            : version;
    }

    private static bool TableExists(DbConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    private static void Execute(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
