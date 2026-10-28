using DotNetBoost.Auditing.Core.Models;
using DotNetBoostSample.Api.Auditing;
using DotNetBoostSample.Application.Settings;
using DotNetBoostSample.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace DotNetBoostSample.Tests.Integration;

/// <summary>
/// Retention, as the <c>auditing</c> settings group states it and <see cref="AuditRetentionService"/>
/// carries it out: nothing is deleted while the group says off, and once it says on, what is older
/// than its retention goes and nothing younger does.
///
/// <para><b>How "older" is made.</b> An entry's time comes from the engine's clock, and a test cannot
/// wait three days for one to age. So the entries a test wants old are moved back with a bulk
/// update straight against the table — which the interceptor never sees, so the move is not itself
/// recorded — and everything else about them is what the API wrote.</para>
///
/// <para><b>Why the prune is called rather than waited for.</b> <c>PruneIfDueAsync</c> is the method
/// the service's timer calls, on the instance the host is running, so calling it is running the
/// service one minute early. The timer may also fire during a test; a prune is idempotent, so the
/// assertions are about what the table holds afterwards rather than about how many a call
/// deleted.</para>
///
/// <para>Every test that turns pruning on turns it off again in a <c>finally</c>: the collection
/// shares one database, and a retention left on would be deleting under the tests that follow.</para>
/// </summary>
public abstract class AuditRetentionTests(SampleApp app)
{
    /// <summary>
    /// The group starts switched off, and publishes the bounds its validator enforces — at least a
    /// day, because a retention of zero would be a delete-everything button.
    /// </summary>
    [RequiresDockerFact]
    public async Task The_group_starts_switched_off_and_publishes_its_bounds()
    {
        var values = await app.Client.GetJsonAsync("/api/settings/auditing");

        Assert.False(values.GetProperty("pruneEnabled").GetBoolean());
        Assert.Equal(365, values.Int("retentionDays"));
        Assert.Equal(24, values.Int("pruneIntervalHours"));

        var schema = await app.Client.GetJsonAsync("/api/settings/auditing/schema");

        var retention = Property(schema, "retentionDays").GetProperty("constraints");
        Assert.Equal(1, retention.Int("min"));
        Assert.Equal(AuditingSettingsValidator.MaxRetentionDays, retention.Int("max"));

        var interval = Property(schema, "pruneIntervalHours").GetProperty("constraints");
        Assert.Equal(1, interval.Int("min"));
        Assert.Equal(AuditingSettingsValidator.MaxPruneIntervalHours, interval.Int("max"));
    }

    /// <summary>
    /// Off means off: an entry three days old survives a check against a group that has not been
    /// turned on, whatever its retention says.
    /// </summary>
    [RequiresDockerFact]
    public async Task Nothing_is_pruned_while_the_group_says_off()
    {
        var client = app.Client.AsActor();
        var id = await client.CreateProductAsync(app.SeededCategoryId, "Kept while off");

        await AgeAsync("Product", id, TimeSpan.FromDays(3));

        Assert.Null(await app.HostedService<AuditRetentionService>().PruneIfDueAsync());

        Assert.Single(await Trail(client, "Product", id));
    }

    /// <summary>
    /// Turned on with a retention of one day: the trail of a product whose entries are three days
    /// old is gone, a product created now keeps its own, and the save that turned pruning on is in
    /// the trail with who made it.
    /// </summary>
    [RequiresDockerFact]
    public async Task Turned_on_it_deletes_what_is_past_retention_and_keeps_the_rest()
    {
        var client = app.Client.AsActor();
        var old = await client.CreateProductAsync(app.SeededCategoryId, "Past retention");
        var recent = await client.CreateProductAsync(app.SeededCategoryId, "Within retention");

        await AgeAsync("Product", old, TimeSpan.FromDays(3));

        try
        {
            await client.PostAsJsonChecked("/api/settings/auditing", new
            {
                pruneEnabled = true,
                retentionDays = 1,
                pruneIntervalHours = 24
            });

            await app.HostedService<AuditRetentionService>().PruneIfDueAsync();

            Assert.Empty(await Trail(client, "Product", old));
            Assert.Single(await Trail(client, "Product", recent));

            // Who changed the retention is recorded like any other group's save — and, being new, it
            // is not among what that retention deletes.
            var turnedOn = (await client.GetJsonAsync(
                    $"/api/audit?entityName={SettingGroupAuditObserver.EntityName}&entityId={nameof(AuditingSettings)}"))
                .GetProperty("entries").EnumerateArray().First();

            Assert.Equal(Http.DefaultActor, turnedOn.Str("actor"));
            Assert.Contains(
                turnedOn.GetProperty("changes").EnumerateArray(),
                change => change.Str("propertyName") == nameof(AuditingSettings.PruneEnabled));
        }
        finally
        {
            await app.ResetAuditingSettingsAsync();
        }
    }

    /// <summary>
    /// Moves every entry about one entity back by <paramref name="age"/>, with a bulk update the
    /// change tracker — and so the audit interceptor — never sees.
    /// </summary>
    private Task AgeAsync(string entityName, int entityId, TimeSpan age) =>
        app.WithDbAsync(db => db.Set<AuditEntry>()
            .Where(e => e.EntityName == entityName && e.EntityId == entityId.ToString())
            .ExecuteUpdateAsync(set => set.SetProperty(e => e.OccurredAt, e => e.OccurredAt - age)));

    /// <summary>One entity's entries, newest first.</summary>
    private static async Task<List<JsonElement>> Trail(HttpClient client, string entityName, int entityId) =>
        [.. (await client.GetJsonAsync($"/api/audit?entityName={entityName}&entityId={entityId}"))
            .GetProperty("entries").EnumerateArray()];

    private static JsonElement Property(JsonElement schema, string name) =>
        schema.GetProperty("properties").EnumerateArray().Single(p => p.Str("name") == name);
}

/// <summary>
/// <inheritdoc cref="AuditRetentionTests" path="/summary/node()[1]"/>
/// <para>Run in the mode the sample ships in: every language is a translation row.</para>
/// </summary>
[Collection(SampleAppCollection.Name)]
public sealed class AuditRetentionTestsInDefaultMode(SampleApp app) : AuditRetentionTests(app);

/// <summary>
/// The same assertions about retention, against the same application booted with
/// <c>MultiLingualOptions.BaseLanguage = "en"</c>.
///
/// <para>Running them twice rather than reasoning about which ones could care is the point: the
/// mode moves content between a column and a translation row, and the claim this sample makes is
/// that no caller can tell. A claim like that is worth more re-run than re-read.</para>
/// </summary>
[Collection(BaseLanguageSampleAppCollection.Name)]
public sealed class AuditRetentionTestsInBaseLanguageMode(BaseLanguageSampleApp app) : AuditRetentionTests(app);
