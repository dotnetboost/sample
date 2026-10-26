using DotNetBoostSample.Api.Authentication;
using System.Net;
using System.Text.Json;
using Xunit;

namespace DotNetBoostSample.Tests.Integration;

/// <summary>
/// What the fourth library actually does here, over HTTP: a row's whole life in the trail, the name
/// of whoever changed it, the entry that outlives the row, and the two places this application's
/// arrangement shows through — translated text that cannot be in a trail, and a trail that is only
/// as wide as the slices that write it.
///
/// <para><b>Why these need the database.</b> Every claim below is a claim about a transaction.
/// That an entry is committed with the change it describes, that a refused save takes its entry
/// with it, that a deleted product leaves its history standing — none of it is visible to a test
/// that reads attributes, and the last two are exactly where a hand-written recording path would
/// go wrong without saying so.</para>
///
/// <para>Each test creates its own product. The tests in a collection share one database and the
/// trail is append-only, so a test that read "the newest entry" of a shared row would be reading
/// whatever ran before it.</para>
/// </summary>
public abstract class AuditTrailTests(SampleApp app)
{
    /// <summary>
    /// Create, change and delete one product, then read its trail: three entries, newest first,
    /// with the values that moved.
    ///
    /// <para>The delete is the one worth staring at. Nothing cascades to the trail — an entry
    /// points at a row by name and key with no foreign key back — so the history is still there
    /// after the row is not, which is the entry people actually come looking for.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_products_whole_life_is_in_the_trail_and_outlives_it()
    {
        var client = app.Client.AsActor();
        var id = await client.CreateProductAsync(app.SeededCategoryId, "Audited product");

        await client.SendJsonCheckedAsync(HttpMethod.Put, $"/api/products/{id}", new
        {
            sku = $"AUD-{id}",
            price = 42.50m,
            categoryId = app.SeededCategoryId,
            translations = new Dictionary<string, Dictionary<string, string>>()
        });

        await client.SendJsonCheckedAsync(HttpMethod.Delete, $"/api/products/{id}");

        // The row is gone.
        Assert.True((await client.GetAsync($"/api/products/{id}")).IsNotFound());

        // Its history is not.
        var entries = await Trail(client, "Product", id);

        Assert.Equal(["Deleted", "Updated", "Created"], entries.Select(e => e.Str("action")));

        // The update says what moved, old value → new. The create and the delete carry the row's
        // initial and final values, which after a delete is the only copy of what it said.
        var price = Changes(entries[1])["Price"];

        Assert.Equal(("1.00", "42.50"), (price.Str("oldValue"), price.Str("newValue")));
        Assert.Equal("42.50", Changes(entries[0])["Price"].Str("oldValue"));
        Assert.Null(Changes(entries[0])["Price"].GetProperty("newValue").GetString());

        // And the key the entry is addressed by is the product's own, formatted the way both other
        // libraries format a key — see KeyFormatAgreementTests for why that is not a coincidence.
        Assert.All(entries, entry => Assert.Equal(id.ToString(), entry.Str("entityId")));
    }

    /// <summary>
    /// <c>GET /api/audit/{id}</c> — one entry, read by the id the page carries, with its changes.
    /// </summary>
    [RequiresDockerFact]
    public async Task One_entry_is_readable_by_its_id()
    {
        var client = app.Client.AsActor();
        var id = await client.CreateProductAsync(app.SeededCategoryId, "Read by id");

        var listed = (await Trail(client, "Product", id)).Single();
        var one = await client.GetJsonAsync($"/api/audit/{listed.Str("id")}");

        Assert.Equal(listed.Str("id"), one.Str("id"));
        Assert.Equal("Created", one.Str("action"));
        Assert.Equal(Changes(listed).Keys.Order(), Changes(one).Keys.Order());
    }

    /// <summary>
    /// The actor is whoever made the change, per request — not a configured name and not the one
    /// reading the trail.
    ///
    /// <para>This is the half of the library that the sample needed an authentication scheme for.
    /// <c>UseHttpContextActor()</c> reads <c>HttpContext.User</c>, so the two writes below are
    /// attributed to the two callers that made them, and a third caller reads both.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task Each_entry_records_the_caller_who_made_that_change()
    {
        var id = await app.Client.AsActor("alice@example.com")
            .CreateProductAsync(app.SeededCategoryId, "Changed by two people");

        await app.Client.AsActor("bob@example.com").SendJsonCheckedAsync(
            HttpMethod.Put, $"/api/products/{id}", new
            {
                sku = $"TWO-{id}",
                price = 7.0m,
                categoryId = app.SeededCategoryId,
                translations = new Dictionary<string, Dictionary<string, string>>()
            });

        var entries = await Trail(app.Client.AsActor("carol@example.com"), "Product", id);

        Assert.Equal(["bob@example.com", "alice@example.com"], entries.Select(e => e.Str("actor")));
    }

    /// <summary>
    /// A write that only changed translated text records nothing, and the model says why.
    ///
    /// <para><c>Name</c> and <c>Description</c> carry <c>[AuditIgnore]</c> because their value
    /// depends on the language and an entry has no language — <see cref="AuditRuleTests"/> pins
    /// the attributes, and this pins what they mean on the wire: an editor saving a product in
    /// Arabic adds no entry, rather than one that says <c>Name</c> changed without saying in which
    /// language.</para>
    ///
    /// <para>It also reads <c>GET /api/audit/entities/{entityName}</c>, which reports the rule as
    /// <c>Ignore</c> — so the model's description and the entries agree. A description that listed
    /// a property the trail never carries would be worse than none.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task Translated_text_is_out_of_the_trail_rather_than_in_it_without_a_language()
    {
        var client = app.Client.AsActor();
        var id = await client.CreateProductAsync(app.SeededCategoryId, "Text only");

        var before = (await Trail(client, "Product", id)).Count;

        // Same columns, new text in two languages.
        await client.SendJsonCheckedAsync(HttpMethod.Put, $"/api/products/{id}", new
        {
            sku = $"TXT-{id}",
            price = 1.0m,
            categoryId = app.SeededCategoryId,
            translations = new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["Name"] = "Renamed", ["Description"] = "Rewritten." },
                ["ar"] = new() { ["Name"] = "مُعاد التسمية", ["Description"] = "أُعيدت كتابته." }
            }
        });

        var after = await Trail(client, "Product", id);

        // The SKU moved, so there IS an entry — and it says only that.
        Assert.Equal(before + 1, after.Count);
        Assert.Equal(["Sku"], Changes(after[0]).Keys);

        // The text did reach the API, which is what makes the absence above a decision rather than
        // a dropped write. Read off the detail response's per-language map, which is every
        // language at once — the shape the editor draws a box per language from.
        Assert.Equal(
            "مُعاد التسمية",
            (await client.GetJsonAsync($"/api/products/{id}"))
                .GetProperty("translations").GetProperty("ar").Str("Name"));

        var described = await client.GetJsonAsync("/api/audit/entities/Product");

        Assert.Equal(
            ["Description", "Name"],
            described.GetProperty("properties").EnumerateArray()
                .Where(p => p.Str("rule") == "Ignore")
                .Select(p => p.Str("name"))
                .Order());
    }

    /// <summary>
    /// A refused save takes its entry with it — the atomicity the explicit recording path was
    /// arranged to keep.
    ///
    /// <para>The update is recorded <em>before</em> the save, and the EF Core store tracks the
    /// entry rather than writing it, so both are in one <c>SaveChanges</c>. Here that save is
    /// refused by the unique index on the SKU: the product keeps its old SKU, the caller gets a
    /// <c>409</c>, and the trail must not have gained an entry claiming a change that never
    /// happened.</para>
    ///
    /// <para>This is the test that would fail if someone "simplified" the slices by recording after
    /// the save instead of before it, and nothing else in the suite would notice.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_refused_write_records_nothing()
    {
        var client = app.Client.AsActor();

        var taken = $"TAKEN-{Guid.NewGuid():N}"[..12];
        await client.CreateProductAsync(app.SeededCategoryId, "Holds the sku");
        await client.SendJsonCheckedAsync(HttpMethod.Post, "/api/products", new
        {
            sku = taken,
            price = 1.0m,
            categoryId = app.SeededCategoryId,
            translations = new Dictionary<string, Dictionary<string, string>>()
        });

        var id = await client.CreateProductAsync(app.SeededCategoryId, "Wants the sku");
        var before = (await Trail(client, "Product", id)).Count;

        var refused = await client.SendJsonAsync(HttpMethod.Put, $"/api/products/{id}", new
        {
            sku = taken,
            price = 99.0m,
            categoryId = app.SeededCategoryId,
            translations = new Dictionary<string, Dictionary<string, string>>()
        });

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(before, (await Trail(client, "Product", id)).Count);
    }

    /// <summary>
    /// A request that names nobody is signed in as the sample's one default user — so the trail is
    /// readable without the header and a change made that way is attributed to that user.
    ///
    /// <para>The audit endpoints still require an authenticated caller — DotNetBoost.Auditing's own
    /// default, kept — the sample scheme simply always provides one.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_caller_who_names_nobody_is_the_default_sample_user()
    {
        // No X-Sample-Actor header.
        var client = app.Client;

        var id = await client.CreateProductAsync(app.SeededCategoryId, "Made by the default user");

        var entries = await Trail(client, "Product", id);

        Assert.Equal(
            SampleActorAuthenticationHandler.DefaultActor,
            Assert.Single(entries).Str("actor"));
    }

    /// <summary>
    /// <c>GET /api/audit/entities</c> — the two entities this application declares, and no library's
    /// own.
    ///
    /// <para>Which is the honest shape of this installation, and worth asserting rather than
    /// explaining: <c>Setting</c>, <c>MediaFile</c> and <c>Translation</c> are rows in the same
    /// database, and they are <em>not</em> in the trail, because the change-tracker capture that
    /// would have recorded them cannot be attached to a context that holds a composite-key entity.
    /// A reader who assumes "one DbContext, so everything is audited" has it wrong, and this is
    /// where they find out.</para>
    ///
    /// <para>It also publishes <c>maxPageSize</c>, so a client can stop asking for more than it
    /// will ever be given rather than discovering the clamp by comparing <c>take</c> with what it
    /// requested.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task The_declared_entities_are_this_applications_own_two()
    {
        var entities = await app.Client.AsActor().GetJsonAsync("/api/audit/entities");

        Assert.Equal(
            ["Category", "Product"],
            entities.GetProperty("entities").EnumerateArray().Select(e => e.Str("name")).Order());

        Assert.All(
            entities.GetProperty("entities").EnumerateArray(),
            entity => Assert.True(entity.GetProperty("audited").GetBoolean()));

        Assert.True(entities.GetProperty("maxPageSize").GetInt32() > 0);
    }

    // ── Reading the trail ────────────────────────────────────────────────────

    /// <summary>One entity's entries, newest first.</summary>
    private static async Task<List<JsonElement>> Trail(HttpClient client, string entityName, int entityId) =>
        [.. (await client.GetJsonAsync($"/api/audit?entityName={entityName}&entityId={entityId}"))
            .GetProperty("entries").EnumerateArray()];

    /// <summary>An entry's changes, by property name — which is how every assertion above asks.</summary>
    private static Dictionary<string, JsonElement> Changes(JsonElement entry) =>
        entry.GetProperty("changes").EnumerateArray()
            .ToDictionary(change => change.Str("propertyName"), change => change);
}

/// <summary>
/// <inheritdoc cref="AuditTrailTests" path="/summary/node()[1]"/>
/// <para>Run in the mode the sample ships in: every language is a translation row.</para>
/// </summary>
[Collection(SampleAppCollection.Name)]
public sealed class AuditTrailTestsInDefaultMode(SampleApp app) : AuditTrailTests(app);

/// <summary>
/// The same assertions against the same application booted with
/// <c>MultiLingualOptions.BaseLanguage = "en"</c>.
///
/// <para>Worth the second run for one reason in particular: with a base language configured,
/// <c>Name</c> and <c>Description</c> are columns of the row being audited rather than translation
/// rows — so the diff that feeds an entry genuinely sees them move, and <c>[AuditIgnore]</c> is
/// what keeps them out. In the other mode they could have been absent by accident.</para>
/// </summary>
[Collection(BaseLanguageSampleAppCollection.Name)]
public sealed class AuditTrailTestsInBaseLanguageMode(BaseLanguageSampleApp app) : AuditTrailTests(app);
