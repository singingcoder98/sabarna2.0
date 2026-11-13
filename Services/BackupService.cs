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

using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Sabarna.Web.Services;

/// <summary>One file in the backup folder, as the admin screen shows it.</summary>
public record BackupFile(string Name, long SizeBytes, DateTime CreatedUtc);

public class BackupService
{
    /// <summary>How long a copy is kept. Fourteen daily copies is plenty for a shop this size.</summary>
    public const int RetentionDays = 14;

    private const string FilePrefix = "sabarna-";
    private const string FileSuffix = ".db";

    // Only names this service itself produces may ever be read back out of the folder.
    private static readonly Regex NamePattern =
        new(@"^sabarna-\d{8}-\d{6}\.db$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _connectionString;
    private readonly string _folder;
    private readonly ILogger<BackupService> _logger;

    public BackupService(IConfiguration configuration, IWebHostEnvironment environment, ILogger<BackupService> logger)
    {
        _connectionString = configuration.GetConnectionString("Default") ?? "Data Source=app.db";

        var builder = new SqliteConnectionStringBuilder(_connectionString);
        var dataSource = string.IsNullOrWhiteSpace(builder.DataSource) ? "app.db" : builder.DataSource;

        var databasePath = Path.IsPathRooted(dataSource)
            ? dataSource
            : Path.Combine(environment.ContentRootPath, dataSource);

        var databaseFolder = Path.GetDirectoryName(databasePath);

        // Backups sit next to the database, so they travel with App_Data and are never published.
        _folder = Path.Combine(databaseFolder ?? environment.ContentRootPath, "backups");

        _logger = logger;
    }

    /// <summary>The folder holding the copies. Shown on the admin screen; never part of the web root.</summary>
    public string Folder => _folder;

    /// <summary>Takes one consistent copy and returns its file name.</summary>
    public async Task<string> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_folder);

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

        return Directory.EnumerateFiles(_folder, FilePrefix + "*" + FileSuffix)
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

    /// <summary>Removes copies older than the retention window. Failures are logged, never thrown.</summary>
    private void PruneOldBackups()
    {
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);

        foreach (var file in Directory.EnumerateFiles(_folder, FilePrefix + "*" + FileSuffix))
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
