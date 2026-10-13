using System.Net;
using System.Text.Json;
using Xunit;

namespace DotNetBoostSample.Tests.Integration;

/// <summary>
/// The same application, booted with <c>MultiLingualOptions.BaseLanguage = "en"</c>.
///
/// <para><b>Why a second container is worth it.</b> Base-language mode is not a flag that changes
/// a response: it changes where content is STORED. A translatable property keeps its column, that
/// column holds English, every other language stays a translation row, an entity may only be
/// created in the base language, and <c>Set("en", …)</c> is refused outright. Four of those five
/// are invisible to every other test in this project, and the fifth is a 500 waiting for the first
/// caller who posts under <c>Accept-Language: ar</c>.</para>
///
/// <para><b>What these tests are really asserting</b> is a negative: that turning the mode on
/// changes nothing a client can see. Same routes, same status codes, same bodies, same keys in the
/// <c>translations</c> map — so the dashboard needs no edit, and neither does any other caller.
/// The one assertion that looks inward is the first, which reads the column directly; without it
/// the rest would pass just as well with the mode off, and prove nothing.</para>
/// </summary>
[Collection(BaseLanguageSampleAppCollection.Name)]
public class BaseLanguageTests(BaseLanguageSampleApp app)
{
    /// <summary>
    /// The mode is actually on: <c>Products</c> has a <c>Name</c> column and the seeded English
    /// text is in it, rather than in a translation row.
    ///
    /// <para>This is the test that makes the others mean something — and the one query in the file
    /// that no caller of the API could make. In the default mode the column does not exist at all,
    /// so this statement would not even parse.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task The_base_language_is_the_entitys_own_column()
    {
        Assert.Equal(
            "Oak writing desk",
            await app.ScalarAsync("""select "Name" from "Products" where "Sku" = 'DESK-001'"""));

        // And it is not ALSO a row. The base language is one store or the other, never both —
        // which is what keeps a stale row from quietly shadowing the column.
        Assert.Equal(
            "0",
            await app.ScalarAsync(
                """select count(*) from "Translations" where "EntityName" = 'Product' and "LanguageCode" = 'en'"""));
    }

    /// <summary>
    /// Reading is unchanged: the request's language, falling back to the base language's column
    /// rather than to a default-language row.
    /// </summary>
    [RequiresDockerFact]
    public async Task A_plain_entity_read_still_comes_back_in_the_requests_language()
    {
        var client = app.Client;

        string NameOf(JsonElement page) =>
            page.GetProperty("items").EnumerateArray().First(p => p.Str("sku") == "DESK-001").Str("name");

        Assert.Equal("Oak writing desk", NameOf(await client.GetJsonAsync("/api/products", "en")));
        Assert.Equal("مكتب كتابة من البلوط", NameOf(await client.GetJsonAsync("/api/products", "ar")));
        Assert.Equal("Bureau en chêne", NameOf(await client.GetJsonAsync("/api/products", "fr")));
    }

    /// <summary>
    /// A create under <c>Accept-Language: ar</c> — which is what the dashboard sends while someone
    /// is reading in Arabic — still answers <c>201</c>, with its sentence in Arabic, and stores the
    /// English text from the body as the base value.
    ///
    /// <para>The library refuses a create outside the base language, for a good reason: the base
    /// value is what every translation hangs off and the last fallback for every other language.
    /// <c>TranslatedText</c> moves the save into the base language instead of letting that refusal
    /// reach the caller — and puts the request's language back, which is why the message below is
    /// Arabic.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_create_read_in_another_language_still_answers_201_in_that_language()
    {
        var client = app.Client;

        var response = await client.SendJsonAsync(HttpMethod.Post, "/api/products", new
        {
            sku = $"BL-{Guid.NewGuid():N}"[..12],
            price = 10.0m,
            categoryId = app.SeededCategoryId,
            translations = new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["Name"] = "Base value", ["Description"] = "English." },
                ["ar"] = new() { ["Name"] = "القيمة الأساسية", ["Description"] = "عربي." }
            }
        }, language: "ar");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.ReadJsonAsync("POST /api/products");

        // The API's own sentence, in the language the request was read in — not in the language
        // the row had to be written in.
        Assert.StartsWith("تم إنشاء المنتج", created.Str("message"));

        var id = created.Int("id");

        // The base value went to the column.
        Assert.Equal(
            "Base value",
            await app.ScalarAsync($"""select "Name" from "Products" where "Id" = {id}"""));

        // And a language nobody wrote falls back to it.
        var french = (await client.GetJsonAsync("/api/products", "fr"))
            .GetProperty("items").EnumerateArray().First(p => p.Int("id") == id);

        Assert.Equal("Base value", french.Str("name"));

        await client.SendJsonAsync(HttpMethod.Delete, $"/api/products/{id}");
    }

    /// <summary>
    /// The detail endpoint still answers with every language in one map, the base language among
    /// them — which is what the dashboard's editor draws a box per language from.
    ///
    /// <para><b>The trap this guards.</b> <c>For(entity).GetAllAsync()</c> answers with stored
    /// translations, and in this mode the base language is not one of them. Left at that, the
    /// editor would open with the English boxes empty and save that emptiness over the canonical
    /// text on the first write — a silent erasure, from a screen that looked right.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_detail_read_carries_the_base_language_beside_the_translations()
    {
        var client = app.Client;

        var id = await client.CreateProductAsync(app.SeededCategoryId, "Editable product");

        await client.SendJsonOkAsync(HttpMethod.Put, $"/api/products/{id}", new
        {
            sku = $"ED-{Guid.NewGuid():N}"[..12],
            price = 5.0m,
            categoryId = app.SeededCategoryId,
            translations = new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["Name"] = "Editable product", ["Description"] = "Created by a test." },
                ["fr"] = new() { ["Name"] = "Produit modifiable", ["Description"] = "Par un test." }
            }
        });

        var detail = await client.GetJsonAsync($"/api/products/{id}");
        var text = detail.GetProperty("translations");

        Assert.Equal("Editable product", text.GetProperty("en").Str("Name"));
        Assert.Equal("Produit modifiable", text.GetProperty("fr").Str("Name"));

        // The round trip the editor actually makes: post back what was read, with one unrelated
        // field changed. The base text has to survive it.
        await client.SendJsonOkAsync(HttpMethod.Put, $"/api/products/{id}", new
        {
            sku = $"ED-{Guid.NewGuid():N}"[..12],
            price = 6.0m,
            categoryId = app.SeededCategoryId,
            translations = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string?>>>(
                text.GetRawText())
        }, language: "fr");

        var again = await client.GetJsonAsync($"/api/products/{id}");

        Assert.Equal("Editable product", again.GetProperty("translations").GetProperty("en").Str("Name"));
        Assert.Equal("Produit modifiable", again.GetProperty("translations").GetProperty("fr").Str("Name"));

        await client.SendJsonAsync(HttpMethod.Delete, $"/api/products/{id}");
    }

    /// <summary>
    /// Updating a product in Arabic without touching its text leaves the English base alone — the
    /// case the library's own documentation names: without the rule that an untouched property is
    /// a borrowed base value, changing only the price under <c>Accept-Language: ar</c> would file
    /// the English name as the Arabic one.
    /// </summary>
    [RequiresDockerFact]
    public async Task Updating_in_another_language_leaves_the_base_text_alone()
    {
        var client = app.Client;

        var id = await client.CreateProductAsync(app.SeededCategoryId, "Untouched base");
        var sku = $"UB-{Guid.NewGuid():N}"[..12];

        await client.SendJsonOkAsync(HttpMethod.Put, $"/api/products/{id}", new
        {
            sku,
            price = 99.0m,
            categoryId = app.SeededCategoryId,
            translations = new Dictionary<string, Dictionary<string, string>>()
        }, language: "ar");

        Assert.Equal(
            "Untouched base",
            await app.ScalarAsync($"""select "Name" from "Products" where "Id" = {id}"""));

        // No Arabic row was invented for it either.
        Assert.Equal(
            "0",
            await app.ScalarAsync(
                $"""
                 select count(*) from "Translations"
                 where "EntityName" = 'Product' and "EntityId" = '{id}' and "LanguageCode" = 'ar'
                 """));

        await client.SendJsonAsync(HttpMethod.Delete, $"/api/products/{id}");
    }

    /// <summary>
    /// A subject — a media file's alt text, branding's store name — still takes every language
    /// including the base one, because a subject has no columns: every language it carries is a
    /// translation row.
    ///
    /// <para>Which is why the dashboard's alt-text editor, which posts all of them, needs no edit
    /// in this mode while the catalogue's editor did.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_subject_still_takes_the_base_language_as_a_translation()
    {
        var client = app.Client;

        var productId = await client.CreateProductAsync(app.SeededCategoryId, "Product with alt text");
        var upload = await client.UploadAsync($"/api/products/{productId}/images?role=primary", "base.png");
        var fileId = upload.Str("id");

        await client.SendJsonOkAsync(HttpMethod.Put, $"/api/translations/MediaFile/{fileId}", new
        {
            translations = new Dictionary<string, Dictionary<string, string?>>
            {
                ["en"] = new() { ["AltText"] = "A desk" },
                ["ar"] = new() { ["AltText"] = "مكتب" }
            }
        });

        var english = await client.GetJsonAsync($"/api/products/{productId}", "en");
        var arabic = await client.GetJsonAsync($"/api/products/{productId}", "ar");

        Assert.Equal("A desk", english.GetProperty("images").EnumerateArray().First().Str("altText"));
        Assert.Equal("مكتب", arabic.GetProperty("images").EnumerateArray().First().Str("altText"));

        await client.SendJsonAsync(HttpMethod.Delete, $"/api/products/{productId}");
    }

    /// <summary>
    /// The other pair of write slices, in one pass: a category created in Arabic and then renamed
    /// in English, with the detail endpoint answering both languages each time.
    /// </summary>
    [RequiresDockerFact]
    public async Task A_category_round_trips_both_languages()
    {
        var client = app.Client;
        var slug = $"bl-{Guid.NewGuid():N}"[..20];

        var created = await client.SendJsonCheckedAsync(HttpMethod.Post, "/api/categories", new
        {
            slug,
            translations = new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["Name"] = "Shelving", ["Description"] = "Shelves." },
                ["ar"] = new() { ["Name"] = "أرفف", ["Description"] = "رفوف." }
            }
        }, language: "ar");

        var id = created.Int("id");

        await client.SendJsonOkAsync(HttpMethod.Put, $"/api/categories/{id}", new
        {
            slug,
            translations = new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["Name"] = "Shelving units", ["Description"] = "Shelves." },
                ["ar"] = new() { ["Name"] = "أرفف", ["Description"] = "رفوف." }
            }
        }, language: "ar");

        var detail = (await client.GetJsonAsync($"/api/categories/{id}")).GetProperty("translations");

        Assert.Equal("Shelving units", detail.GetProperty("en").Str("Name"));
        Assert.Equal("أرفف", detail.GetProperty("ar").Str("Name"));

        // The rename reached the column, not an "en" row.
        Assert.Equal(
            "Shelving units",
            await app.ScalarAsync($"""select "Name" from "Categories" where "Id" = {id}"""));

        // Checked rather than fired and forgotten: a delete is a save of a translatable entity
        // too, and this mode is where the save path changed.
        await client.SendJsonOkAsync(HttpMethod.Delete, $"/api/categories/{id}");

        Assert.Equal(
            "0",
            await app.ScalarAsync(
                $"""select count(*) from "Translations" where "EntityName" = 'Category' and "EntityId" = '{id}'"""));
    }

    /// <summary>
    /// Branding is unaffected, and for a reason worth stating: its <c>[MultiLingual]</c> store name
    /// is not an entity with columns, it is a settings row with translation rows beside it. The
    /// base language has nothing to do with a group that is not in the EF model, so
    /// <c>GET /api/settings/branding</c> answers exactly as it does in the other mode — the stored
    /// value, and the request's language resolved beside it.
    /// </summary>
    [RequiresDockerFact]
    public async Task Branding_still_resolves_its_translated_name_beside_the_stored_one()
    {
        var client = app.Client;

        await client.PostAsJsonChecked("/api/settings/branding", new
        {
            storeName = "Base Store",
            accentColor = "#512BD4",
            headerLogoSize = 28
        });

        await client.SendJsonOkAsync(HttpMethod.Put, "/api/translations/Branding/branding", new
        {
            translations = new Dictionary<string, object>
            {
                ["ar"] = new Dictionary<string, string?> { ["StoreName"] = "متجر أساسي" }
            }
        });

        var arabic = await client.GetJsonAsync("/api/settings/branding", "ar");

        Assert.Equal("متجر أساسي", arabic.GetProperty("resolved").Str("storeName"));
        Assert.Equal("Base Store", arabic.Str("storeName"));
    }

    /// <summary>
    /// The one difference a caller can see, pinned here rather than left to be discovered: content
    /// created without any text in the base language reads as the empty string in a language
    /// nobody translated, where the default mode answers <c>null</c>.
    ///
    /// <para>It is the honest consequence of a column: a translatable property on these entities is
    /// a non-nullable <c>string</c>, so "nothing was ever written" has an empty value rather than no
    /// row. Both modes show the same thing on a screen — nothing — and neither is reachable from
    /// the dashboard, which always posts the languages its editor collected.</para>
    ///
    /// <para>The library would rather this never happened: a create is supposed to carry the base
    /// language, which is what <c>BaseLanguageRequiredException</c> is about. This application does
    /// not refuse it, because refusing would be a new 400 on an endpoint that accepts it today —
    /// and a response that changed is the one thing turning this mode on is not allowed to do.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task Content_created_without_the_base_language_reads_as_empty_rather_than_refused()
    {
        var client = app.Client;

        var created = await client.SendJsonCheckedAsync(HttpMethod.Post, "/api/products", new
        {
            sku = $"NB-{Guid.NewGuid():N}"[..12],
            price = 1.0m,
            categoryId = app.SeededCategoryId,
            translations = new Dictionary<string, Dictionary<string, string>>
            {
                ["ar"] = new() { ["Name"] = "بلا أساس", ["Description"] = "لا إنجليزية." }
            }
        }, language: "ar");

        var id = created.Int("id");

        var arabic = (await client.GetJsonAsync("/api/products", "ar"))
            .GetProperty("items").EnumerateArray().First(p => p.Int("id") == id);

        var french = (await client.GetJsonAsync("/api/products", "fr"))
            .GetProperty("items").EnumerateArray().First(p => p.Int("id") == id);

        Assert.Equal("بلا أساس", arabic.Str("name"));
        Assert.Equal("", french.Str("name"));

        await client.SendJsonAsync(HttpMethod.Delete, $"/api/products/{id}");
    }

    /// <summary>
    /// A refused write is still a refusal — 400, 404 or 409 as before, and in the caller's
    /// language.
    ///
    /// <para>The one assertion here that is really about this mode is the last. The save runs
    /// inside the base language, and the refusal is raised by the database in the middle of that —
    /// so the language has to be back before <c>DbUpdateExceptionHandler</c> looks the sentence up.
    /// That is what the <c>finally</c> in <c>TranslatedText</c> is for, and a test that only
    /// checked the status code would pass with it deleted.</para>
    /// </summary>
    [RequiresDockerFact]
    public async Task A_refused_write_keeps_its_status_and_its_language()
    {
        var client = app.Client;
        var sku = $"DUP-{Guid.NewGuid():N}"[..12];

        var id = (await client.SendJsonCheckedAsync(HttpMethod.Post, "/api/products", new
        {
            sku,
            price = 1.0m,
            categoryId = app.SeededCategoryId,
            translations = new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["Name"] = "First", ["Description"] = "Has the SKU." }
            }
        })).Int("id");

        // The same SKU again, read in Arabic. The unique index refuses it.
        var refused = await client.SendJsonAsync(HttpMethod.Post, "/api/products", new
        {
            sku,
            price = 2.0m,
            categoryId = app.SeededCategoryId,
            translations = new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["Name"] = "Second", ["Description"] = "Wants the same SKU." }
            }
        }, language: "ar");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("يستخدم منتج آخر رمز المنتج هذا بالفعل.", await refused.ProblemDetailAsync());

        // And a category that does not exist is still a 404 rather than a foreign-key 500.
        var missing = await client.SendJsonAsync(HttpMethod.Post, "/api/products", new
        {
            sku = $"NC-{Guid.NewGuid():N}"[..12],
            price = 1.0m,
            categoryId = 987654,
            translations = new Dictionary<string, Dictionary<string, string>>()
        });

        Assert.True(missing.IsNotFound());

        // And a request that is malformed rather than refused by the database is still a 400 with
        // its per-field errors — ValidationBehavior throws before any of this mode's machinery is
        // reached, which is the answer to "what did base-language mode change here": nothing.
        var invalid = await client.SendJsonAsync(HttpMethod.Post, "/api/products", new
        {
            sku = "",
            price = -1m,
            categoryId = app.SeededCategoryId,
            translations = new Dictionary<string, Dictionary<string, string>>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("errors", (await invalid.ProblemJsonAsync()).Keys());

        await client.SendJsonOkAsync(HttpMethod.Delete, $"/api/products/{id}");
    }

    /// <summary>
    /// Deleting the entity takes its text with it — the columns go with the row, and the other
    /// languages' rows go through MultiLingual's SaveChanges interceptor, which sees the delete.
    /// </summary>
    [RequiresDockerFact]
    public async Task Deleting_an_entity_takes_every_language_with_it()
    {
        var client = app.Client;

        var id = await client.CreateProductAsync(app.SeededCategoryId, "Doomed product");

        await client.SendJsonOkAsync(HttpMethod.Put, $"/api/products/{id}", new
        {
            sku = $"DL-{Guid.NewGuid():N}"[..12],
            price = 1.0m,
            categoryId = app.SeededCategoryId,
            translations = new Dictionary<string, Dictionary<string, string>>
            {
                ["ar"] = new() { ["Name"] = "محكوم عليه", ["Description"] = "سيُحذف." }
            }
        });

        Assert.Equal("2", await TranslationCountAsync(id));

        await client.SendJsonOkAsync(HttpMethod.Delete, $"/api/products/{id}");

        Assert.Equal("0", await TranslationCountAsync(id));
        Assert.Null(await app.ScalarAsync($"""select "Name" from "Products" where "Id" = {id}"""));
    }

    /// <summary>
    /// The other cascade, which is this application's own: a deleted media file takes its alt text
    /// with it, through <c>MediaTextCleanup</c>. Subject rows, so nothing about them changes here —
    /// asserted because "nothing changes" is the claim this whole file is making.
    /// </summary>
    [RequiresDockerFact]
    public async Task Deleting_a_file_still_takes_its_alt_text()
    {
        var client = app.Client;

        var productId = await client.CreateProductAsync(app.SeededCategoryId, "Product with a doomed image");
        var upload = await client.UploadAsync($"/api/products/{productId}/images?role=primary", "gone.png");
        var fileId = upload.Str("id");

        await client.SendJsonOkAsync(HttpMethod.Put, $"/api/translations/MediaFile/{fileId}", new
        {
            translations = new Dictionary<string, Dictionary<string, string?>>
            {
                ["en"] = new() { ["AltText"] = "About to go" }
            }
        });

        await client.SendJsonOkAsync(HttpMethod.Delete, $"/api/media/{fileId}");

        Assert.Equal(
            "0",
            await app.ScalarAsync(
                $"""select count(*) from "Translations" where "EntityName" = 'MediaFile' and "EntityId" = '{fileId}'"""));

        await client.SendJsonOkAsync(HttpMethod.Delete, $"/api/products/{productId}");
    }

    /// <summary>
    /// The language administration the dashboard's Languages page drives: purging a language's
    /// translations, and the same button pressed on the base language — whose values are columns and
    /// therefore not translations to purge. Neither is allowed to be a 500.
    /// </summary>
    [RequiresDockerFact]
    public async Task Purging_a_languages_translations_behaves_for_both_kinds_of_language()
    {
        var client = app.Client;

        var purged = await client.SendJsonAsync(HttpMethod.Delete, "/api/languages/fr/translations");

        Assert.True(purged.IsSuccessStatusCode, await purged.Content.ReadAsStringAsync());

        // French is gone from the rows, so it now reads as the base language's column.
        Assert.Equal(
            "0",
            await app.ScalarAsync("""select count(*) from "Translations" where "LanguageCode" = 'fr'"""));

        Assert.Equal(
            "Oak writing desk",
            (await client.GetJsonAsync("/api/products", "fr"))
                .GetProperty("items").EnumerateArray().First(p => p.Str("sku") == "DESK-001").Str("name"));

        // The base language either refuses or reports nothing to purge; what it must not do is
        // take the columns with it.
        var baseLanguage = await client.SendJsonAsync(HttpMethod.Delete, "/api/languages/en/translations");

        Assert.NotEqual(HttpStatusCode.InternalServerError, baseLanguage.StatusCode);

        Assert.Equal(
            "Oak writing desk",
            await app.ScalarAsync("""select "Name" from "Products" where "Sku" = 'DESK-001'"""));
    }

    /// <summary>
    /// The Settings × MultiLingual join still publishes <c>translated</c> for a settings property
    /// carrying <c>[MultiLingual]</c>. It is read off the attribute rather than out of the store, so
    /// the mode cannot reach it — asserted for the same reason as the test above.
    /// </summary>
    [RequiresDockerFact]
    public async Task The_settings_schema_still_flags_the_translated_property()
    {
        var schema = await app.Client.GetJsonAsync("/api/settings/branding/schema");

        var storeName = schema.GetProperty("properties")
            .EnumerateArray()
            .First(p => p.Str("name").Equals("storeName", StringComparison.OrdinalIgnoreCase));

        Assert.True(storeName.GetProperty("constraints").GetProperty("translated").GetBoolean());
    }

    /// <summary>Translation rows for one product, as the database has them.</summary>
    private Task<string?> TranslationCountAsync(int productId) =>
        app.ScalarAsync(
            $"""
             select count(*) from "Translations"
             where "EntityName" = 'Product' and "EntityId" = '{productId}'
             """);

    /// <summary>
    /// The base language cannot be removed, and the refusal is the library's own problem detail
    /// rather than a 500 — so the dashboard's Languages page shows a sentence, as it does for every
    /// other refusal.
    /// </summary>
    [RequiresDockerFact]
    public async Task The_base_language_cannot_be_deleted()
    {
        var response = await app.Client.SendJsonAsync(HttpMethod.Delete, "/api/languages/en");

        Assert.InRange((int)response.StatusCode, 400, 499);
        Assert.NotEqual("", await response.ProblemDetailAsync());

        // Still there, and still the default.
        var languages = await app.Client.GetJsonAsync("/api/languages");

        Assert.Contains(languages.EnumerateArray(), l => l.Str("code") == "en");
    }
}

/// <summary>
/// The sample, booted in base-language mode. One more container than the suite needs, and the only
/// way to run the second half of what <c>ApplyMultiLingual</c> can do: the mode is a schema
/// decision, so it cannot be switched inside a running application.
/// </summary>
public sealed class BaseLanguageSampleApp : SampleApp
{
    /// <inheritdoc/>
    protected override string? BaseLanguage => "en";
}

/// <inheritdoc cref="SampleAppCollection"/>
[CollectionDefinition(Name)]
public sealed class BaseLanguageSampleAppCollection : ICollectionFixture<BaseLanguageSampleApp>
{
    public const string Name = "sample-app-base-language";
}
