// saptarshi: Phase 0 (12-Nov-2026) — this whole file is new.
// -------------------------------------------------------------------------------------------------
// Backups, because the whole shop lives in one file.
//
// WHAT IT DOES
//   • takes a copy of app.db into App_Data/backups/sabarna-YYYYMMDD-HHmmss.db
//   • keeps the last 14 days and quietly deletes anything older
//   • lets the admin download any of those copies from /Admin/Backups
//
// WHY THE COPY IS SAFE WHILE THE SHOP IS RUNNING
//   A plain File.Copy of a live SQLite file can capture a half-written page and produce a file that
//   opens but is quietly broken. This uses SQLite's own online backup API (BackupDatabase), which
//   copies page by page under a read lock: the copy is a consistent snapshot of a moment, taken while
//   orders are still coming in.
// -------------------------------------------------------------------------------------------------

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sabarna.Web.Data;

namespace Sabarna.Web.Services;

/// <summary>One file in the backup folder, as the admin screen shows it.</summary>
public record BackupFile(string Name, long SizeBytes, DateTime CreatedUtc);

public class BackupService
{
    /// <summary>How long a copy is kept. Fourteen daily copies is plenty for a shop this size.</summary>
    public const int RetentionDays = 14;

    private const string FilePrefix = "sabarna-";
    private const string FileSuffix = ".db";

    // saptarshi: 18-Nov-2026 — a PostgreSQL shop cannot be copied as a file, so its backup is a JSON
    // export instead (see CreateJsonSnapshotAsync). Same prefix, different extension.
    private const string SnapshotSuffix = ".json";

    // Only names this service itself produces may ever be read back out of the folder.
    private static readonly Regex NamePattern =
        new(@"^sabarna-\d{8}-\d{6}\.(db|json)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _connectionString;
    private readonly string _folder;
    private readonly ILogger<BackupService> _logger;

    // saptarshi: 18-Nov-2026 — the JSON export needs a DbContext, and this service is a singleton,
    // so it asks for a fresh scope each time instead of holding a connection open.
    private readonly IServiceScopeFactory _scopes;

    private readonly bool _isPostgres;

    public BackupService(IConfiguration configuration, IWebHostEnvironment environment,
                         ILogger<BackupService> logger, IServiceScopeFactory scopes)
    {
        _connectionString = configuration.GetConnectionString("Default")
                            ?? DatabaseSetup.DefaultSqliteConnectionString;

        _isPostgres = DatabaseSetup.Resolve(configuration) == DatabaseKind.Postgres;
        _scopes = scopes;
        _logger = logger;

        if (_isPostgres)
        {
            // There is no database file to sit next to, so the snapshots live in the one folder that
            // is already excluded from publishing and already excluded from the FileZilla upload.
            _folder = Path.Combine(environment.ContentRootPath, "App_Data", "backups");
            return;
        }

        var builder = new SqliteConnectionStringBuilder(_connectionString);
        var dataSource = string.IsNullOrWhiteSpace(builder.DataSource) ? "app.db" : builder.DataSource;

        var databasePath = Path.IsPathRooted(dataSource)
            ? dataSource
            : Path.Combine(environment.ContentRootPath, dataSource);

        var databaseFolder = Path.GetDirectoryName(databasePath);

        // Backups sit next to the database, so they travel with App_Data and are never published.
        _folder = Path.Combine(databaseFolder ?? environment.ContentRootPath, "backups");
    }

    /// <summary>
    /// saptarshi: 18-Nov-2026 — true when the shop runs on PostgreSQL. The admin screen uses this to
    /// explain that copies are now JSON snapshots rather than a .db file.
    /// </summary>
    public bool IsPostgres => _isPostgres;

    /// <summary>The folder holding the copies. Shown on the admin screen; never part of the web root.</summary>
    public string Folder => _folder;

    /// <summary>Takes one consistent copy and returns its file name.</summary>
    public async Task<string> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_folder);

        // saptarshi: 18-Nov-2026 — PostgreSQL: export the rows instead of copying a file. A database
        // server keeps its data in a directory the application must never touch, and pg_dump is not
        // installed on a shared host, so a readable JSON export is the honest backup this app can make
        // by itself. The provider (Neon, Supabase, your host) keeps the real point-in-time copies.
        if (_isPostgres)
        {
            return await CreateJsonSnapshotAsync(cancellationToken);
        }

        var name = FilePrefix + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + FileSuffix;
        var target = Path.Combine(_folder, name);

        await using (var source = new SqliteConnection(_connectionString))
        await using (var destination = new SqliteConnection(
                         new SqliteConnectionStringBuilder { DataSource = target }.ToString()))
        {
            await source.OpenAsync(cancellationToken);
            await destination.OpenAsync(cancellationToken);

            // The consistent copy. Orders can keep arriving while this runs.
            source.BackupDatabase(destination);
        }

        PruneOldBackups();

        _logger.LogInformation("Database backup written: {BackupFile}", name);
        return name;
    }

    /// <summary>Newest first, ready for the table.</summary>
    public IReadOnlyList<BackupFile> ListBackups()
    {
        if (!Directory.Exists(_folder))
        {
            return Array.Empty<BackupFile>();
        }

        return Directory.EnumerateFiles(_folder, FilePrefix + "*")
            .Where(file => NamePattern.IsMatch(Path.GetFileName(file)))
            .Select(file => new FileInfo(file))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .Select(info => new BackupFile(info.Name, info.Length, info.LastWriteTimeUtc))
            .ToList();
    }

    /// <summary>
    /// Full path of a backup, or null when the name is not one of ours. The name check is what stops
    /// a crafted request from walking out of the folder (../../web.config).
    /// </summary>
    public string ResolvePath(string name)
    {
        if (!IsSafeName(name))
        {
            return null;
        }

        var path = Path.Combine(_folder, name);
        return File.Exists(path) ? path : null;
    }

    public static bool IsSafeName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && NamePattern.IsMatch(name);
    }

    /// <summary>
    /// True when the newest copy is more than twenty hours old. The scheduled job asks this first, so
    /// a host that recycles the app pool ten times a day still produces about one copy a day.
    /// </summary>
    public bool ShouldTakeBackupNow()
    {
        var newest = ListBackups().FirstOrDefault();
        return newest == null || newest.CreatedUtc < DateTime.UtcNow.AddHours(-20);
    }

    /// <summary>
    /// saptarshi: 18-Nov-2026 — the PostgreSQL backup: every table written to one readable JSON file.
    ///
    /// It is not a byte-for-byte database copy (that needs pg_dump, which a shared host does not have),
    /// but it holds every row the shop cares about — settings, products, orders with their items and
    /// the payment audit trail — so nothing a customer typed can ever be lost with the server. The file
    /// can be kept on a PC, opened in Notepad, or imported back with a short script.
    /// </summary>
    public async Task<string> CreateJsonSnapshotAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var snapshot = new
        {
            exportedAtUtc = DateTime.UtcNow,
            database = "PostgreSQL",
            settings = await db.Settings.AsNoTracking().ToListAsync(cancellationToken),
            products = await db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync(cancellationToken),
            orders = await db.Orders.AsNoTracking()
                .Include(order => order.Items)
                .OrderBy(order => order.Id)
                .ToListAsync(cancellationToken),
            paymentEvents = await db.PaymentEvents.AsNoTracking()
                .OrderBy(e => e.Id)
                .ToListAsync(cancellationToken)
        };

        var name = FilePrefix + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + SnapshotSuffix;
        var target = Path.Combine(_folder, name);

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        });

        await File.WriteAllTextAsync(target, json, cancellationToken);

        PruneOldBackups();

        _logger.LogInformation("PostgreSQL snapshot written: {BackupFile}", name);
        return name;
    }

    /// <summary>Removes copies older than the retention window. Failures are logged, never thrown.</summary>
    private void PruneOldBackups()
    {
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);

        foreach (var file in Directory.EnumerateFiles(_folder, FilePrefix + "*"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                    _logger.LogInformation("Old backup removed: {BackupFile}", Path.GetFileName(file));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not remove the old backup {BackupFile}.", Path.GetFileName(file));
            }
        }
    }
}
