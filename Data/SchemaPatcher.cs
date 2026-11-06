// saptarshi: added 06-Nov-2026 — this whole file is new.
// -------------------------------------------------------------------------------------------------
// SchemaPatcher — bringing an ALREADY EXISTING app.db up to date, without losing a single order.
//
// WHY THIS EXISTS
// The shop uses EnsureCreated(), which only builds the tables when the database file does not exist
// yet. The database on the live server does exist — it has real orders in it. Adding properties to
// the Order class therefore does NOT add columns to that file, and the first query would crash with
// "SQLite Error 1: no such column".
//
// So on every start we:
//   1. read the columns that really are in the file (PRAGMA table_info),
//   2. add only the ones that are missing (ALTER TABLE ... ADD COLUMN),
//   3. create the two new tables if they are not there (CREATE TABLE IF NOT EXISTS).
//
// Nothing is ever dropped, renamed or rewritten. Running it twice is harmless, which matters because
// a shared host recycles the app pool and runs this again and again.
// -------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Sabarna.Web.Data;

public static class SchemaPatcher
{
    /// <summary>The new columns on the Orders table, with the SQL type SQLite needs.</summary>
    private static readonly (string Name, string Definition)[] OrderColumns =
    {
        ("Address",            "TEXT NOT NULL DEFAULT ''"),
        ("Phone",              "TEXT NOT NULL DEFAULT ''"),
        ("Note",               "TEXT NOT NULL DEFAULT ''"),
        ("RazorpayOrderId",    "TEXT NOT NULL DEFAULT ''"),
        ("RazorpayPaymentId",  "TEXT NOT NULL DEFAULT ''"),
        ("RazorpaySignature",  "TEXT NOT NULL DEFAULT ''"),
        ("PaymentStatus",      "TEXT NOT NULL DEFAULT 'Paid'"),   // old rows were recorded as paid
        ("PaymentVerifiedBy",  "TEXT NOT NULL DEFAULT ''"),
        ("PaidAt",             "TEXT NULL"),
        ("FailureReason",      "TEXT NOT NULL DEFAULT ''"),
        ("RefundedAt",         "TEXT NULL"),
        ("RefundAmount",       "TEXT NOT NULL DEFAULT '0'"),
        ("TaxAmount",          "TEXT NOT NULL DEFAULT '0'"),
        ("Gstin",              "TEXT NOT NULL DEFAULT ''"),
        ("InvoiceNumber",      "TEXT NOT NULL DEFAULT ''"),
        ("CourierName",        "TEXT NOT NULL DEFAULT ''"),
        ("AwbNumber",          "TEXT NOT NULL DEFAULT ''"),
        ("ShippedAt",          "TEXT NULL"),
        ("DeliveredAt",        "TEXT NULL")
    };

    private const string OrderItemsTable = @"
CREATE TABLE IF NOT EXISTS ""OrderItems"" (
    ""Id""        INTEGER NOT NULL CONSTRAINT ""PK_OrderItems"" PRIMARY KEY AUTOINCREMENT,
    ""OrderId""   INTEGER NOT NULL,
    ""ProductId"" INTEGER NOT NULL,
    ""Name""      TEXT NULL,
    ""Qty""       INTEGER NOT NULL,
    ""UnitPrice"" TEXT NOT NULL DEFAULT '0',
    ""LineTotal"" TEXT NOT NULL DEFAULT '0',
    CONSTRAINT ""FK_OrderItems_Orders_OrderId"" FOREIGN KEY (""OrderId"")
        REFERENCES ""Orders"" (""Id"") ON DELETE CASCADE
);";

    private const string PaymentEventsTable = @"
CREATE TABLE IF NOT EXISTS ""PaymentEvents"" (
    ""Id""                INTEGER NOT NULL CONSTRAINT ""PK_PaymentEvents"" PRIMARY KEY AUTOINCREMENT,
    ""EventId""           TEXT NULL,
    ""EventName""         TEXT NULL,
    ""OrderNumber""       TEXT NULL,
    ""PaymentId""         TEXT NULL,
    ""SignatureVerified"" INTEGER NOT NULL DEFAULT 0,
    ""Payload""           TEXT NULL,
    ""CreatedAt""         TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'
);";

    /// <summary>Adds whatever the database is missing. Safe to call on every start.</summary>
    public static async Task EnsureAsync(AppDbContext db, ILogger logger)
    {
        try
        {
            // ---- 1. Orders: add the columns that are not there yet -------------------------------
            var existing = await ColumnNamesAsync(db, "Orders");

            if (existing.Count == 0)
            {
                // No Orders table at all — EnsureCreated has just made a complete one, so there is
                // nothing to patch. (Both paths are normal; this is the fresh-install path.)
                logger.LogInformation("SchemaPatcher: Orders table not found yet, nothing to patch.");
                return;
            }

            var added = 0;

            // saptarshi: CA2100 is about SQL injection. The only thing that reaches this string is
            // the hard-coded list above — no user input, ever — so the warning is silenced here.
#pragma warning disable CA2100
            foreach (var column in OrderColumns)
            {
                if (existing.Contains(column.Name))
                {
                    continue;
                }

                var sql = $@"ALTER TABLE ""Orders"" ADD COLUMN ""{column.Name}"" {column.Definition}";
                await db.Database.ExecuteSqlRawAsync(sql);
                added++;
            }

            // ---- 2. The two new tables ----------------------------------------------------------
            await db.Database.ExecuteSqlRawAsync(OrderItemsTable);
            await db.Database.ExecuteSqlRawAsync(PaymentEventsTable);

            // One payment can only ever belong to one order. The partial index ignores the empty
            // strings that unpaid orders carry, so those do not collide with each other.
            await db.Database.ExecuteSqlRawAsync(
                @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Orders_RazorpayPaymentId""
                  ON ""Orders"" (""RazorpayPaymentId"")
                  WHERE ""RazorpayPaymentId"" IS NOT NULL AND ""RazorpayPaymentId"" <> '';");

            await db.Database.ExecuteSqlRawAsync(
                @"CREATE INDEX IF NOT EXISTS ""IX_Orders_RazorpayOrderId""
                  ON ""Orders"" (""RazorpayOrderId"");");
#pragma warning restore CA2100

            if (added > 0)
            {
                logger.LogInformation("SchemaPatcher: added {Count} column(s) to Orders.", added);
            }
        }
        catch (Exception ex)
        {
            // Never stop the shop from starting because a patch failed — but say so loudly, because
            // the next screen that reads a missing column will fail.
            logger.LogError(ex, "SchemaPatcher: could not bring the database up to date.");
        }
    }

    /// <summary>Column names of a table, read straight from SQLite. Empty when the table is absent.</summary>
    private static async Task<HashSet<string>> ColumnNamesAsync(AppDbContext db, string table)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;

        if (openedHere)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{table}\");";

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // PRAGMA table_info columns: cid, name, type, notnull, dflt_value, pk
                names.Add(reader.GetString(1));
            }
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }

        return names;
    }
}
