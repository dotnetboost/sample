using DotNetBoost.Auditing.Core.Models;
using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.Settings.Core.Interfaces;
using DotNetBoostSample.Api.Authentication;
using DotNetBoostSample.Application.Settings;
using DotNetBoostSample.Domain.Catalog;
using DotNetBoostSample.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
            // An identity key: the INSERT assigns it, and the Created entry is written in a save
            // nested inside this one — see CategoryConfiguration.
            await db.Categories.AddAsync(category);
            await db.SaveChangesAsync();
            Assert.True(category.Id > 0);

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
    /// to a second save of its own. A key generated by the INSERT gives the audit interceptor a
    /// reason to make one — its nested save for the <c>Created</c> entry, which is exactly what
    /// Category and Product's identity keys now cause — and this reads every language back after
    /// it.</para>
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
    /// <see cref="SettingGroupAuditParticipant"/> records the save, addressed as
    /// <c>("Setting", &lt;group name&gt;)</c> — under the ignored name, which is why the participant
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
    /// A settings group's entry commits in the caller's own save, on the caller's own context.
    ///
    /// <para><see cref="SettingGroupAuditParticipant"/> runs inside the settings write, before the
    /// store saves, and records on the scope's <see cref="AppDbContext"/> — the one the settings
    /// store saves. So the entry is tracked there afterwards, already saved, with nothing left
    /// pending: one save carried the rows and the entry.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_settings_entry_commits_in_the_callers_own_save()
    {
        try
        {
            await app.WithScopeAsync(async services =>
            {
                var db = services.GetRequiredService<AppDbContext>();

                await services.GetRequiredService<ISettingManager>()
                    .For<CatalogSettings>()
                    .SetAsync(new CatalogSettings { PageSize = 19, CurrencySymbol = "¥" });

                // The entry went out through the caller's context, in its save, and nothing waits.
                Assert.Equal(
                    EntityState.Unchanged,
                    Assert.Single(db.ChangeTracker.Entries<AuditEntry>()).State);
                Assert.False(db.ChangeTracker.HasChanges());
            });

            // And the entry is in the trail.
            var newest = (await GroupTrail(app.Client.AsActor()))[0];

            Assert.Equal("19", Changes(newest)["PageSize"].Str("newValue"));
        }
        finally
        {
            await app.ResetCatalogSettingsAsync();
        }
    }

    /// <summary>
    /// A settings write that fails after its entry was recorded leaves the group unchanged, no
    /// entry in the trail, and nothing pending on the caller's context.
    ///
    /// <para>The guarantee the participant gives and the observer before it could not. The write is
    /// failed by <see cref="SettingsWriteSabotage"/>, a test-only participant that runs after the
    /// application's — so the entry and its change rows have been enlisted when it throws, and the
    /// settings library has to take all of them back. The last save is the trap: had a change row
    /// been left <c>Added</c>, the context's next unrelated save would insert it.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_settings_write_that_fails_leaves_the_group_and_the_trail_as_they_were()
    {
        var client = app.Client.AsActor();
        var entriesBefore = (await GroupTrail(client)).Count;
        var before = await client.GetJsonAsync("/api/settings/catalog");

        await app.WithScopeAsync(async services =>
        {
            var db = services.GetRequiredService<AppDbContext>();
            services.GetRequiredService<SettingsWriteSabotage>().Armed = true;

            await Assert.ThrowsAsync<SettingsWriteSabotagedException>(() =>
                services.GetRequiredService<ISettingManager>()
                    .For<CatalogSettings>()
                    .SetAsync(new CatalogSettings { PageSize = 23, CurrencySymbol = "₩" }));

            // Neither the entry nor any of its change rows is left for the next save to insert.
            Assert.False(db.ChangeTracker.HasChanges());
            Assert.Equal(0, await db.SaveChangesAsync());
        });

        Assert.Equal(entriesBefore, (await GroupTrail(client)).Count);
        Assert.Equal(before.Int("pageSize"), (await client.GetJsonAsync("/api/settings/catalog")).Int("pageSize"));
    }

    /// <summary>
    /// A product image's whole life, in the trails people actually read: the upload under the file,
    /// the attach in the product's own history — <c>Gallery: — → lamp.png</c>, the operation in the
    /// entry's metadata — and, when the product is deleted, the detach on the product and the
    /// delete under the file. None of DotNetBoost.Media's own rows are in the trail.
    ///
    /// <para>The interceptor alone recorded this as a <c>MediaAttachment</c> entry addressed by the
    /// link's own GUID, and a soft delete as a <c>MediaFile</c> <c>Updated</c> entry about
    /// <c>DeletedAt</c>; the product's trail said nothing. <c>IgnoreEntity&lt;MediaFile&gt;()</c>
    /// and <c>IgnoreEntity&lt;MediaAttachment&gt;()</c> keep the rows out, and
    /// <see cref="MediaAuditParticipant"/> records each operation as DotNetBoost.Media reports it —
    /// under the product, because Media's owner address and Auditing's entity address are the same
    /// <c>(entityName, entityId)</c>.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_product_images_life_is_in_the_product_and_file_trails()
    {
        var client = app.Client.AsActor("media.editor@example.com");
        var id = await client.CreateProductAsync(app.SeededCategoryId, "Has a photo");

        var upload = await client.UploadAsync($"/api/products/{id}/images?role=gallery", "lamp.png");
        var fileId = upload.Str("id");

        // The attach, in the product's own history, by whoever made it.
        var attached = Assert.Single(await MediaTrail(client, "Product", id));
        Assert.Equal("Updated", attached.Str("action"));
        Assert.Equal("media.editor@example.com", attached.Str("actor"));
        Assert.Equal(("Gallery", null, "lamp.png"), Row(attached));
        Assert.Equal(("Attach", fileId), (Operation(attached).Str("media"), Operation(attached).Str("fileId")));

        // The upload, under the file — and the attach shares its correlation id: one call.
        var uploaded = Assert.Single(await FileTrail(client, fileId));
        Assert.Equal("Created", uploaded.Str("action"));
        Assert.Equal("lamp.png", Changes(uploaded)["FileName"].Str("newValue"));
        Assert.DoesNotContain("Tags", Changes(uploaded).Keys);   // no tags, so no empty row
        Assert.Equal(uploaded.Str("correlationId"), attached.Str("correlationId"));

        // Deleting the product deletes the image only it held: one delete under the file, and a
        // detach on the product that says why.
        await client.SendJsonCheckedAsync(HttpMethod.Delete, $"/api/products/{id}");

        var detached = (await MediaTrail(client, "Product", id)).Single(e => Operation(e).Str("media") == "Detach");
        Assert.Equal(("Gallery", "lamp.png", null), Row(detached));
        Assert.Equal("FileDeleted", Operation(detached).Str("reason"));

        var fileTrail = await FileTrail(client, fileId);
        Assert.Equal(["Created", "Deleted"], fileTrail.Select(e => e.Str("action")).Order());

        var deleted = fileTrail.Single(e => e.Str("action") == "Deleted");
        Assert.Equal("lamp.png", Changes(deleted)["FileName"].Str("oldValue"));
        Assert.Equal(deleted.Str("correlationId"), detached.Str("correlationId"));

        // And not one of the media library's rows, from this test or any other.
        Assert.Equal("0", await app.ScalarAsync(
            "SELECT COUNT(*) FROM \"AuditEntries\" WHERE \"EntityName\" IN ('MediaFile', 'MediaAttachment')"));
    }

    /// <summary>
    /// Replacing a single-valued role and reordering a gallery are one entry each in the product's
    /// history: <c>Primary: first.png → second.png</c>, and <c>Gallery</c> with the whole order before
    /// and after — not a detach and an attach, and not one entry per image that moved.
    /// </summary>
    [RequiresDockerFact]
    public async Task A_replacement_and_a_reorder_are_one_entry_each()
    {
        var client = app.Client.AsActor();
        var id = await client.CreateProductAsync(app.SeededCategoryId, "Rearranged");

        var first = (await client.UploadAsync($"/api/products/{id}/images?role=primary", "first.png")).Str("id");
        var a     = (await client.UploadAsync($"/api/products/{id}/images?role=gallery", "a.png")).Str("id");
        var b     = (await client.UploadAsync($"/api/products/{id}/images?role=gallery", "b.png")).Str("id");
        var second = Guid.Parse((await client.UploadAsync("/api/media", "second.png")).Str("id"));

        await app.WithScopeAsync(async services =>
        {
            var media = services.GetRequiredService<IMediaManager>().For("Product", id.ToString());

            await media.SetAsync("primary", second);
            await media.ReorderAsync("gallery", [Guid.Parse(b), Guid.Parse(a)]);
        });

        var trail = await MediaTrail(client, "Product", id);

        var replaced = Assert.Single(trail, e => Operation(e).Str("media") == "Replace");
        Assert.Equal(("Primary", "first.png", "second.png"), Row(replaced));
        Assert.Equal(
            (second.ToString("D"), first),
            (Operation(replaced).Str("fileId"), Operation(replaced).Str("previousFileId")));

        var reordered = Assert.Single(trail, e => Operation(e).Str("media") == "Reorder");
        Assert.Equal(("Gallery", "a.png, b.png", "b.png, a.png"), Row(reordered));
    }

    /// <summary>
    /// A media write that fails after its entry was recorded leaves the file unattached, no entry in
    /// the trail, and nothing pending on the caller's context.
    ///
    /// <para>The media counterpart of the settings test above. <see cref="MediaWriteSabotage"/> runs
    /// after <see cref="MediaAuditParticipant"/>, so the entry and its change row are enlisted when
    /// it throws, and DotNetBoost.Media has to take them back. The last save is the trap: an entry
    /// left <c>Added</c> would be inserted by the context's next unrelated save.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_media_write_that_fails_leaves_the_link_and_the_trail_as_they_were()
    {
        var client = app.Client.AsActor();
        var id = await client.CreateProductAsync(app.SeededCategoryId, "Refuses a photo");
        var fileId = Guid.Parse((await client.UploadAsync("/api/media", "refused.png")).Str("id"));
        var before = (await Trail(client, "Product", id)).Count;

        await app.WithScopeAsync(async services =>
        {
            var db = services.GetRequiredService<AppDbContext>();
            services.GetRequiredService<MediaWriteSabotage>().Armed = true;

            await Assert.ThrowsAsync<MediaWriteSabotagedException>(() =>
                services.GetRequiredService<IMediaManager>().For("Product", id.ToString()).AddAsync(fileId, "gallery"));

            Assert.False(db.ChangeTracker.HasChanges());
            Assert.Equal(0, await db.SaveChangesAsync());
        });

        Assert.Equal(before, (await Trail(client, "Product", id)).Count);
        Assert.Equal("0", await app.ScalarAsync(
            $"SELECT COUNT(*) FROM \"MediaAttachments\" WHERE \"MediaFileId\" = '{fileId}'"));
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
    /// wider — <c>Language</c> is recorded too, by the interceptor, a settings group by its
    /// participant, as <see cref="A_settings_save_is_one_entry_for_the_group"/> shows, and a media
    /// file under <c>Media</c> by its. And <c>Translation</c>, <c>MediaFile</c> and
    /// <c>MediaAttachment</c> are neither: ignored, so they cannot be declared, which <c>Build()</c>
    /// enforces.</para>
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

    // ── Failures inside a creation's save ────────────────────────────────────

    /// <summary>
    /// A creation whose translation write fails leaves nothing behind: not the product, not its
    /// other languages' text, not its <c>Created</c> entry.
    ///
    /// <para>The case the identity keys bring in. The INSERT generates the product's id, so
    /// MultiLingual writes the translation rows and Auditing writes the <c>Created</c> entry after
    /// it, each from its own interceptor, in the transaction that covers the save. Before
    /// Auditing 1.0.0-preview.3, with Auditing registered first, a failure in MultiLingual's write
    /// left the product and its entry committed. A trigger on the translations table makes that
    /// write fail, for this test's marker only, and is dropped afterwards.</para>
    ///
    /// <para>Checked against preview.2: with Auditing registered first and no retrying strategy,
    /// the product was left behind and this failed in both modes. Under the retrying strategy this
    /// sample runs with, MultiLingual replays the save in a transaction of its own and preview.2
    /// passed it too, so here it guards the composition rather than reproducing the old bug.</para>
    ///
    /// <para>The marker is in the Arabic text because Arabic is a translation row in both modes;
    /// in base-language mode the English text is a column of the product.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_creation_whose_translation_write_fails_leaves_nothing_behind()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var sku = $"FAIL-{tag}";
        var marker = $"fail-{tag}";

        await InstallTranslationTriggerAsync(tag, $"""
            IF NEW."Value" = '{marker}' THEN
                RAISE EXCEPTION 'Translation write refused by the test';
            END IF;
            """);

        try
        {
            var response = await app.Client.AsActor().SendJsonAsync(HttpMethod.Post, "/api/products", new
            {
                sku,
                price = 1.0m,
                categoryId = app.SeededCategoryId,
                translations = new Dictionary<string, Dictionary<string, string>>
                {
                    ["en"] = new() { ["Name"] = "Never saved", ["Description"] = "Rolled back." },
                    ["ar"] = new() { ["Name"] = marker, ["Description"] = "لن يحفظ." }
                }
            }, language: "en");

            Assert.False(response.IsSuccessStatusCode, $"The create answered {(int)response.StatusCode}.");

            Assert.Equal("0", await app.ScalarAsync($"SELECT COUNT(*) FROM \"Products\" WHERE \"Sku\" = '{sku}'"));
            Assert.Equal("0", await app.ScalarAsync(
                $"SELECT COUNT(*) FROM \"Translations\" WHERE \"Value\" IN ('{marker}', 'Never saved', 'Rolled back.')"));
            Assert.Equal("0", await CreatedEntriesForSkuAsync(sku));
        }
        finally
        {
            await DropTranslationTriggerAsync(tag);
        }
    }

    /// <summary>
    /// A creation that hits a transient failure is retried by EF's retrying strategy, and recorded
    /// once: one product, its text in every language, and one <c>Created</c> entry, under the id
    /// the successful attempt got.
    ///
    /// <para>The context runs under <c>EnableRetryOnFailure</c>, and MultiLingual replays a save
    /// under a retrying strategy. Before Auditing 1.0.0-preview.3 the replay was captured on top of
    /// the first attempt: every change was recorded twice, and again on each retry. The trigger
    /// raises a serialization failure, which Npgsql's strategy treats as transient, the first time
    /// it sees this test's marker only. It counts with a sequence, because a sequence's value
    /// survives the rollback of the attempt that failed.</para>
    ///
    /// <para>What it proves is the real retry path: with the strategy turned off the create
    /// answers 500. It does not reproduce preview.2's double recording, which in this composition
    /// hit every save but a failed one; the tests above catch that.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_creation_retried_after_a_transient_failure_is_recorded_once()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var sku = $"RETRY-{tag}";
        var marker = $"retry-{tag}";
        var attempts = $"retry_attempts_{tag}";

        await app.ScalarAsync($"CREATE SEQUENCE \"{attempts}\"");
        await InstallTranslationTriggerAsync(tag, $"""
            IF NEW."Value" = '{marker}' AND nextval('"{attempts}"') = 1 THEN
                RAISE EXCEPTION 'A transient failure, forced by the test' USING ERRCODE = '40001';
            END IF;
            """);

        try
        {
            var created = await app.Client.AsActor().SendJsonCheckedAsync(HttpMethod.Post, "/api/products", new
            {
                sku,
                price = 1.0m,
                categoryId = app.SeededCategoryId,
                translations = new Dictionary<string, Dictionary<string, string>>
                {
                    ["en"] = new() { ["Name"] = "Saved on the retry", ["Description"] = "Second attempt." },
                    ["ar"] = new() { ["Name"] = marker, ["Description"] = "المحاولة الثانية." }
                }
            }, language: "en");

            var id = created.Int("id");

            // The trigger ran twice: the failed attempt and the retry. Without this the test would
            // pass just as well if nothing had been retried at all.
            Assert.Equal("2", await app.ScalarAsync($"SELECT last_value FROM \"{attempts}\""));

            Assert.Equal("1", await app.ScalarAsync($"SELECT COUNT(*) FROM \"Products\" WHERE \"Sku\" = '{sku}'"));

            var text = (await app.Client.AsActor().GetJsonAsync($"/api/products/{id}")).GetProperty("translations");
            Assert.Equal("Saved on the retry", text.GetProperty("en").Str("Name"));
            Assert.Equal(marker, text.GetProperty("ar").Str("Name"));

            Assert.Equal("Created", Assert.Single(await Trail(app.Client.AsActor(), "Product", id)).Str("action"));
            Assert.Equal("1", await CreatedEntriesForSkuAsync(sku));
        }
        finally
        {
            await DropTranslationTriggerAsync(tag);
            await app.ScalarAsync($"DROP SEQUENCE IF EXISTS \"{attempts}\"");
        }
    }

    /// <summary>
    /// A row trigger on MultiLingual's translations table running <paramref name="body"/> before
    /// each insert or update — named for <paramref name="tag"/>, so a test drops only its own.
    /// </summary>
    private Task InstallTranslationTriggerAsync(string tag, string body) =>
        app.ScalarAsync($"""
            CREATE FUNCTION "translation_check_{tag}"() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                {body}
                RETURN NEW;
            END $$;
            CREATE TRIGGER "translation_check_{tag}" BEFORE INSERT OR UPDATE ON "Translations"
                FOR EACH ROW EXECUTE FUNCTION "translation_check_{tag}"();
            """);

    private Task DropTranslationTriggerAsync(string tag) =>
        app.ScalarAsync($"""
            DROP TRIGGER IF EXISTS "translation_check_{tag}" ON "Translations";
            DROP FUNCTION IF EXISTS "translation_check_{tag}"();
            """);

    /// <summary>
    /// How many <c>Created</c> entries for a product record this SKU — asked of the tables, so an
    /// entry left under an id the trail's API would never be asked about still counts.
    /// </summary>
    private async Task<string?> CreatedEntriesForSkuAsync(string sku) =>
        await app.ScalarAsync($"""
            SELECT COUNT(*) FROM "AuditEntries" e
            JOIN "AuditPropertyChanges" c ON c."AuditEntryId" = e."Id"
            WHERE e."EntityName" = 'Product' AND e."Action" = 0
              AND c."PropertyName" = 'Sku' AND c."NewValue" = '{sku}'
            """);

    // ── What the suite runs under ────────────────────────────────────────────

    /// <summary>
    /// The context these tests save through retries on failure, so every assertion above about how
    /// many entries a save left is made under the retrying strategy production runs with — the
    /// combination that recorded each change twice before Auditing 1.0.0-preview.3.
    /// </summary>
    [RequiresDockerFact]
    public async Task The_context_saves_under_a_retrying_strategy() =>
        await app.WithDbAsync(db =>
        {
            Assert.True(db.Database.CreateExecutionStrategy().RetriesOnFailure);
            return Task.CompletedTask;
        });

    /// <summary>
    /// A save whose audit entries can no longer commit with it fails here rather than logging an
    /// error: the test host sets <c>Auditing:OnLostAtomicity</c> to <c>Throw</c>. Asserted, because
    /// a typo in the key would leave the default in place and nothing else would notice.
    /// </summary>
    [RequiresDockerFact]
    public async Task The_suite_fails_a_save_that_loses_atomicity() =>
        await app.WithScopeAsync(services =>
        {
            Assert.Equal(
                LostAtomicity.Throw,
                services.GetRequiredService<DotNetBoost.Auditing.Core.AuditingOptions>().OnLostAtomicity);
            return Task.CompletedTask;
        });

    // ── Reading the trail ────────────────────────────────────────────────────

    /// <summary>The catalog settings group's entries, newest first.</summary>
    private static async Task<List<JsonElement>> GroupTrail(HttpClient client) =>
        [.. (await client.GetJsonAsync(
                $"/api/audit?entityName={SettingGroupAuditParticipant.EntityName}&entityId={nameof(CatalogSettings)}"))
            .GetProperty("entries").EnumerateArray()];

    /// <summary>An entity's media entries — the ones carrying an operation in their metadata — newest first.</summary>
    private static async Task<List<JsonElement>> MediaTrail(HttpClient client, string entityName, int entityId) =>
        [.. (await Trail(client, entityName, entityId)).Where(e => e.TryGetProperty("metadata", out var m)
                                                                 && m.ValueKind == JsonValueKind.String)];

    /// <summary>The operation a media entry records, from its metadata.</summary>
    private static JsonElement Operation(JsonElement entry) =>
        JsonDocument.Parse(entry.Str("metadata")).RootElement;

    /// <summary>A media entry's one row, as (property, old, new).</summary>
    private static (string, string?, string?) Row(JsonElement entry)
    {
        var change = Assert.Single(entry.GetProperty("changes").EnumerateArray());
        return (change.Str("propertyName"),
                change.GetProperty("oldValue").GetString(),
                change.GetProperty("newValue").GetString());
    }

    /// <summary>A media file's own entries, newest first.</summary>
    private static async Task<List<JsonElement>> FileTrail(HttpClient client, string fileId) =>
        [.. (await client.GetJsonAsync($"/api/audit?entityName={MediaAuditParticipant.FileEntityName}&entityId={fileId}"))
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
