// saptarshi: Phase 0 (12-Nov-2026) — this whole file is new.
// A tiny background job that keeps one fresh copy of the database on disk, without a scheduler on the
// host. It starts two minutes after the app comes up (so it never slows a cold start) and then looks
// again every six hours; BackupService itself decides whether a new copy is actually due.

namespace Sabarna.Web.Services;

public class BackupHostedService : BackgroundService
{
    private static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly BackupService _backups;
    private readonly ILogger<BackupHostedService> _logger;

    public BackupHostedService(BackupService backups, ILogger<BackupHostedService> logger)
    {
        _backups = backups;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstRunDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;   // the app is shutting down before the first run; nothing to do
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_backups.ShouldTakeBackupNow())
                {
                    await _backups.CreateBackupAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failed backup must never take the shop down; it is logged and retried next time.
                _logger.LogError(ex, "Scheduled database backup failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
