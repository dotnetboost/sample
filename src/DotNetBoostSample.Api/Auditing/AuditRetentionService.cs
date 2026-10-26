using DotNetBoost.Auditing.Core.Interfaces;
using DotNetBoost.Settings.Core.Interfaces;
using DotNetBoostSample.Application.Settings;

namespace DotNetBoostSample.Api.Auditing;

/// <summary>
/// Deletes audit entries past the retention the <see cref="AuditingSettings"/> group states, on the
/// interval it states — the "hosted service calling PruneAsync" that DotNetBoost.Auditing leaves
/// to the application, and that this one used to say it did not have.
///
/// <para><b>Why it is in the host.</b> Running something on a timer is a fact about the process,
/// and the host is the only project that owns one — <c>AddInfrastructure</c> is deliberately kept
/// off <c>Microsoft.Extensions.Hosting</c>. It is also the one place both libraries' abstractions
/// are already referenced, through their <c>*.API</c> packages.</para>
///
/// <para><b>Not a join.</b> It reads a number from one library and hands it to another, the way
/// <c>ListProducts</c> reads a page size from Settings beside the files it reads from Media. Neither
/// library's subject is said to be the other's, so it is not on the README's list of the files where
/// the composition happens.</para>
///
/// <para><b>Why it reads the group every minute rather than sleeping for the interval.</b> The group
/// is runtime-editable, and a service that read it once and slept for twenty-four hours would apply
/// a shortened retention, or a prune switched off, a day late. The read is a cache hit in
/// DotNetBoost.Settings unless the group was just saved, so the minute costs nothing; the interval
/// is measured from the last prune instead. Turning pruning on prunes at the next minute, and
/// turning it off and on again starts the interval over.</para>
///
/// <para><b>What it does not use.</b> <c>AuditingOptions.Retention</c> and
/// <c>PruneAsync()</c>: the option is read once at <c>Build()</c>, so it cannot follow the group.
/// <c>PruneOlderThanAsync</c> takes the age as an argument instead — at the cost that it ignores
/// a per-entity <c>[AuditRetention]</c>, which nothing in this application declares. If something
/// ever does, this has to prune it separately and exclude it here.</para>
///
/// <para><b>One process.</b> Nothing stops two instances of the API from pruning at once. The
/// prune is idempotent and batched, so that costs a wasted sweep rather than a wrong answer — but a
/// deployment with many replicas would want this behind a lease, or in a job of its own.</para>
/// </summary>
public sealed partial class AuditRetentionService(
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<AuditRetentionService> logger) : BackgroundService
{
    /// <summary>How often the group is read to see whether a prune is due.</summary>
    public static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(1);

    /// <summary>When the last prune ran, or null when none has since pruning was last turned on.</summary>
    private DateTimeOffset? _lastPruned;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckEvery, time);

        do
        {
            try
            {
                await PruneIfDueAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Logged and carried on: a database that was briefly unreachable must not stop
                // retention for the life of the process. The next minute tries again, and a prune
                // that was cut short resumes where it stopped — the work is idempotent.
                LogPruneFailed(ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// Reads the group and prunes if it says to and the interval has passed.
    ///
    /// <para>Public so a test can run one check without waiting for the timer. It is the same method
    /// the timer calls, so what the test proves is what runs.</para>
    /// </summary>
    /// <returns>How many entries were deleted, or null when no prune was due.</returns>
    public async Task<int?> PruneIfDueAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();

        var settings = await scope.ServiceProvider
            .GetRequiredService<ISettingManager>()
            .For<AuditingSettings>()
            .GetAsync(cancellationToken: cancellationToken);

        if (!settings.PruneEnabled)
        {
            _lastPruned = null;
            return null;
        }

        var now = time.GetUtcNow();

        if (_lastPruned is { } last && now - last < TimeSpan.FromHours(settings.PruneIntervalHours))
            return null;

        var deleted = await scope.ServiceProvider
            .GetRequiredService<IAuditManager>()
            .PruneOlderThanAsync(TimeSpan.FromDays(settings.RetentionDays), ct: cancellationToken);

        _lastPruned = now;
        LogPruned(deleted, settings.RetentionDays);

        return deleted;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Pruned {Deleted} audit entries older than {RetentionDays} days.")]
    private partial void LogPruned(int deleted, int retentionDays);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Pruning the audit trail failed; the next check will try again.")]
    private partial void LogPruneFailed(Exception exception);
}
