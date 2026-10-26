using DotNetBoostSample.Api.Authentication;
using DotNetBoostSample.Application.Settings;
using DotNetBoostSample.Domain.Catalog;
using DotNetBoostSample.Infrastructure.Persistence;
using System.Net;
using System.Text.Json;
using Xunit;

namespace DotNetBoostSample.Tests.Integration;

/// <summary>
/// What the fourth library actually does here, over HTTP: a row's whole life in the trail, the name
/// of whoever changed it, the entry that outlives the row, and the places this application's
/// arrangement shows through — translated text that cannot be in a trail, two sibling libraries'
/// rows that are kept out of it, and the text a creation must not lose on the way in.
///
/// <para><b>Why these need the database.</b> Every claim below is a claim about a transaction.
/// That an entry is committed with the change it describes, that a refused save takes its entry
/// with it, that a deleted product leaves its history standing, that a creation's entry is written
/// without a second save — none of it is visible to a test that reads attributes.</para>
///
/// <para>Nothing in the catalogue's handlers records anything: every entry below is written by
/// DotNetBoost.Auditing's SaveChanges interceptor, reading the change tracker — except a settings
/// group's, which DotNetBoost.Settings reports a save of as a whole. The second test
/// makes that point without the handlers at all.</para>
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
    /// The same three entries from a context nobody wrote a line of auditing for: a category added,
    /// changed and removed straight through <c>AppDbContext</c>, with no slice in the way.
    ///
    /// <para>Which is the claim the interceptor makes — every change this context saves — and the
    /// one the hand-written path this application used to have could not: a write that did not go
    /// through one of the six slices recorded nothing. There is no request here, so there is no
    /// actor either, and the entries say so rather than inventing one.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_change_saved_straight_through_the_context_is_recorded_by_the_interceptor()
    {
        var category = new Category { Slug = $"direct-{Guid.NewGuid():N}"[..20] };

        await app.WithDbAsync(async db =>
        {
            // AddAsync assigns the HiLo id, so it is known before the save — see CategoryConfiguration.
            await db.Categories.AddAsync(category);
            Assert.True(category.Id > 0);
            await db.SaveChangesAsync();

            category.Slug += "-renamed";
            await db.SaveChangesAsync();

            db.Categories.Remove(category);
            await db.SaveChangesAsync();
        });

        var entries = await Trail(app.Client.AsActor(), "Category", category.Id);

        Assert.Equal(["Deleted", "Updated", "Created"], entries.Select(e => e.Str("action")));
        Assert.Equal(["Slug"], Changes(entries[1]).Keys);
        Assert.Equal(category.Slug, Changes(entries[1])["Slug"].Str("newValue"));
        Assert.All(entries, entry => Assert.True(
            !entry.TryGetProperty("actor", out var actor) || actor.ValueKind == JsonValueKind.Null));
    }

    /// <summary>
    /// A product saved with text in three languages: one save, one <c>Created</c> entry — and none
    /// for the translation rows saved beside it.
    ///
    /// <para><c>Translation</c> is DotNetBoost.MultiLingual's entity, keyed on four columns, and an
    /// audit entry records one. Before <c>IgnoreEntity&lt;Translation&gt;()</c> the interceptor
    /// threw on exactly this save, which is every catalogue write; now the rows are skipped as
    /// ignored. Asked of the table rather than of one product's trail, so a translation recorded
    /// under any address at all — the seed's included — fails it.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task Translation_rows_saved_with_a_product_are_not_in_the_trail()
    {
        var client = app.Client.AsActor();

        var created = await client.SendJsonCheckedAsync(HttpMethod.Post, "/api/products", new
        {
            sku = $"TRL-{Guid.NewGuid():N}"[..12],
            price = 5.0m,
            categoryId = app.SeededCategoryId,
            translations = new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["Name"] = "Three languages", ["Description"] = "In English." },
                ["ar"] = new() { ["Name"] = "ثلاث لغات", ["Description"] = "بالعربية." },
                ["fr"] = new() { ["Name"] = "Trois langues", ["Description"] = "En français." }
            }
        });

        var id = created.Int("id");

        // The translations were saved — this is not a test that passes because nothing was written.
        Assert.NotEqual("0", await app.ScalarAsync(
            $"SELECT COUNT(*) FROM \"Translations\" WHERE \"EntityName\" = 'Product' AND \"EntityId\" = '{id}'"));

        Assert.Equal("0", await app.ScalarAsync(
            "SELECT COUNT(*) FROM \"AuditEntries\" WHERE \"EntityName\" = 'Translation'"));

        Assert.Empty((await client.GetJsonAsync("/api/audit?entityName=Translation"))
            .GetProperty("entries").EnumerateArray());

        Assert.Equal("Created", Assert.Single(await Trail(client, "Product", id)).Str("action"));
    }

    /// <summary>
    /// A creation keeps the text it was created with, in every language — read back after the
    /// save that wrote its <c>Created</c> entry.
    ///
    /// <para>What it guards is the one place the two libraries' interceptors could step on each
    /// other. A product created through the API never has its text assigned to the instance — it
    /// goes to translation rows — so any later save that re-reads the instance as the request's
    /// language's text writes <c>""</c> over them. That is how this application once lost a name,
    /// to a second save of its own; a key generated by the INSERT would give the audit interceptor
    /// a reason to make one. HiLo keys keep a creation to one save, and this reads every language
    /// back after it.</para>
    ///
    /// <para>Posted in a language, as the dashboard does: with no language resolved, MultiLingual
    /// has no request language to file an instance's text under, and the case cannot arise.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_creation_keeps_its_text_in_every_language()
    {
        var client = app.Client.AsActor();

        var created = await client.SendJsonCheckedAsync(HttpMethod.Post, "/api/products", new
        {
            sku = $"KEEP-{Guid.NewGuid():N}"[..12],
            price = 3.0m,
            categoryId = app.SeededCategoryId,
            translations = new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["Name"] = "Kept in English", ["Description"] = "Still here." },
                ["ar"] = new() { ["Name"] = "محفوظ بالعربية", ["Description"] = "ما زال هنا." }
            }
        }, language: "en");

        var id = created.Int("id");

        // Every language at once, from the detail response's per-language map — the stored rows,
        // not a fallback.
        var text = (await client.GetJsonAsync($"/api/products/{id}")).GetProperty("translations");

        Assert.Equal("Kept in English", text.GetProperty("en").Str("Name"));
        Assert.Equal("Still here.", text.GetProperty("en").Str("Description"));
        Assert.Equal("محفوظ بالعربية", text.GetProperty("ar").Str("Name"));

        // And as a plain read in the request's language, which is the path the interceptors serve.
        Assert.Equal(
            "محفوظ بالعربية",
            (await client.GetJsonAsync("/api/products?pageSize=200", "ar"))
                .GetProperty("items").EnumerateArray().Single(p => p.Int("id") == id).Str("name"));

        // And the entry the creation was recorded with is there too — the text survived the save
        // that wrote it, not a save that skipped it.
        Assert.Equal("Created", Assert.Single(await Trail(client, "Product", id)).Str("action"));
    }

    /// <summary>
    /// A settings save is in the trail as ONE <c>Setting</c> entry for the group, with every value
    /// that moved, by whoever made it — and none of DotNetBoost.Settings' own rows are.
    ///
    /// <para>The group is stored one row per property, so the interceptor alone would record a
    /// save as an entry per row. <c>IgnoreEntity&lt;Setting&gt;()</c> keeps the rows out and
    /// <see cref="SettingGroupAuditObserver"/> records the save, addressed as
    /// <c>("Setting", &lt;group name&gt;)</c> — under the ignored name, which is why the observer
    /// gives the write the group's type. Two values are changed at once here, because one entry
    /// holding both is the whole point.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_settings_save_is_one_entry_for_the_group()
    {
        var client = app.Client.AsActor("settings.editor@example.com");
        // A row's entry would be addressed by the row's id; a group's is addressed by its name.
        var settingRows =
            "SELECT COUNT(*) FROM \"AuditEntries\" WHERE \"EntityName\" = 'Setting' "
            + $"AND \"EntityId\" NOT IN ('{nameof(CatalogSettings)}', '{nameof(BrandingSettings)}')";

        try
        {
            // Twice, because the first write of a group may be the one that CREATES its rows; the
            // second can only be an update, and of the two values that moved.
            await client.PostAsJsonChecked("/api/settings/catalog", new { pageSize = 17, currencySymbol = "€" });
            var before = await GroupTrail(client);

            await client.PostAsJsonChecked("/api/settings/catalog", new { pageSize = 18, currencySymbol = "£" });
            var after = await GroupTrail(client);

            Assert.Equal(before.Count + 1, after.Count);

            var newest = after[0];

            Assert.Equal("Updated", newest.Str("action"));
            Assert.Equal("settings.editor@example.com", newest.Str("actor"));
            Assert.Equal(["CurrencySymbol", "PageSize"], Changes(newest).Keys.Order());
            Assert.Equal(("17", "18"), (Changes(newest)["PageSize"].Str("oldValue"), Changes(newest)["PageSize"].Str("newValue")));
            Assert.Equal(("€", "£"), (Changes(newest)["CurrencySymbol"].Str("oldValue"), Changes(newest)["CurrencySymbol"].Str("newValue")));

            // And the rows behind it are not in the trail at all — not from these saves, and not
            // from the seed's: every Setting entry there is is a group's.
            Assert.Equal("0", await app.ScalarAsync(settingRows));
        }
        finally
        {
            await app.ResetCatalogSettingsAsync();
        }
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
    /// A refused save takes its entry with it — the atomicity the interceptor exists to give.
    ///
    /// <para>The interceptor records the update while the save is being prepared, and the EF Core
    /// store tracks the entry rather than writing it, so both are in one <c>SaveChanges</c>. Here
    /// that save is refused by the unique index on the SKU: the product keeps its old SKU, the
    /// caller gets a <c>409</c>, and the trail must not have gained an entry claiming a change that
    /// never happened.</para>
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
    /// <para>Declared is not the same as recorded, and this is where the difference shows. The
    /// list is what <c>DeclareEntities</c> named, so it is the application's own two; the trail is
    /// wider — <c>MediaFile</c> and the rest are recorded too, by the interceptor, and a settings
    /// group by its observer, as <see cref="A_settings_save_is_one_entry_for_the_group"/> shows. And <c>Translation</c> is
    /// neither: ignored, so it cannot be declared, which <c>Build()</c> enforces.</para>
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

    /// <summary>The catalog settings group's entries, newest first.</summary>
    private static async Task<List<JsonElement>> GroupTrail(HttpClient client) =>
        [.. (await client.GetJsonAsync(
                $"/api/audit?entityName={SettingGroupAuditObserver.EntityName}&entityId={nameof(CatalogSettings)}"))
            .GetProperty("entries").EnumerateArray()];

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
