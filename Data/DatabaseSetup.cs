using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Sabarna.Web.Data;

/// <summary>
/// saptarshi: 18-Nov-2026 — one place that decides which database the shop talks to.
///
/// Until now the answer was hard-coded: a SQLite file called app.db next to the application. That is
/// a fine home for a shop with one owner and a few hundred orders a year — it needs no server, no
/// password and no monthly bill. It stops being fine the moment the shop wants what a real database
/// server gives for free: point-in-time restore, two people in the admin at once, or a host whose
/// disk does not keep data between deployments.
///
/// So the choice is now configuration, not code:
///
///     "Database": { "Provider": "Sqlite" }      ← default, unchanged behaviour
///     "Database": { "Provider": "Postgres" }    ← Npgsql provider, connection string from
///                                                  ConnectionStrings:Default
///
/// On the host this is one environment variable, so the switch needs no new build:
///
///     Database__Provider          = Postgres
///     ConnectionStrings__Default  = Host=…;Port=5432;Database=sabarna;Username=…;Password=…;SSL Mode=Require
/// </summary>
public static class DatabaseSetup
{
    /// <summary>What the app used before Postgres existed. Kept as the default on purpose.</summary>
    public const string DefaultSqliteConnectionString = "Data Source=app.db";

    /// <summary>Reads "Database:Provider". Anything unrecognised quietly means SQLite.</summary>
    public static DatabaseKind Resolve(IConfiguration configuration)
    {
        var raw = configuration.GetValue<string>("Database:Provider");

        if (string.IsNullOrWhiteSpace(raw))
        {
            return DatabaseKind.Sqlite;
        }

        var value = raw.Trim();

        if (value.Equals("Postgres", StringComparison.OrdinalIgnoreCase)
            || value.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseKind.Postgres;
        }

        return DatabaseKind.Sqlite;
    }

    /// <summary>Registers the DbContext for the chosen provider.</summary>
    public static void Configure(DbContextOptionsBuilder options, DatabaseKind kind, string connectionString)
    {
        if (kind == DatabaseKind.Postgres)
        {
            // saptarshi: Npgsql 8 maps DateTime to "timestamp with time zone". Every date this app
            // writes comes from DateTime.UtcNow (AppDbContext.StampTimestamps), so the values are
            // already UTC and nothing has to be converted by hand.
            options.UseNpgsql(connectionString);
            return;
        }

        options.UseSqlite(connectionString);
    }

    /// <summary>
    /// True when the open context is really talking to PostgreSQL.
    ///
    /// This asks the provider itself rather than re-reading configuration, so it stays correct even
    /// if a connection string is overridden somewhere else in the pipeline.
    /// </summary>
    public static bool IsPostgres(DatabaseFacade database)
    {
        return database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
    }
}

/// <summary>Which database engine the shop is configured to use.</summary>
public enum DatabaseKind
{
    Sqlite,
    Postgres
}
