using DotNetBoost.Media.API;
using DotNetBoost.MultiLingual.API;
using DotNetBoostSample.Api.Data;
using DotNetBoostSample.Api.Endpoints;
using DotNetBoostSample.Api.Integration;
using DotNetBoostSample.Api.Settings;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

// ─────────────────────────────────────────────────────────────────────────────
//  DotNetBoost sample — three independent libraries in one application.
//
//  Settings, Media and MultiLingual are installed side by side. Each is configured on its own
//  terms, in its own block. There is no AddDotNetBoost(), no shared kernel package, and no
//  integration package pairing any two of them: see Directory.Packages.props for the full list of
//  what is installed, and tests/ for the assertion that none of them references another.
//
//  What ties them together is five files, all of them application code, none of them large, and
//  each one a place where this application says two libraries are talking about the same thing:
//
//    • Data/AppDbContext.cs            — all three. One database, three Apply* calls, nothing else.
//    • Integration/MediaText.cs        — Media × MultiLingual. Reads a file's per-language alt text
//                                        for this API's own responses, via the (entityName,
//                                        entityId) convention both libraries address a subject by.
//    • Integration/MediaTextCleanup.cs — Media → MultiLingual. Removes that text when Media says
//                                        the file is gone.
//    • Settings/MultiLingualSchemaContributor.cs — Settings × MultiLingual. Publishes `translated`
//                                        on a settings property carrying [MultiLingual], which the
//                                        settings library cannot know about.
//    • Settings/BrandingSettings.cs    — all three, in BrandingProjector. Answers one settings
//                                        group with its text in the request's language and its
//                                        logo.
//
//  Three of those are registered in the blocks below — each in its own library's block, one line
//  each, which is what makes them look like configuration rather than coordination. The count is
//  five rather than a rounder number because every file joining two libraries is on the list; a
//  sixth would mean the claim had quietly stopped being true.
//
//  It was more. Writing alt text is not here any more — DotNetBoost.MultiLingual ships the
//  translation endpoints now, and the dashboard's editor calls them directly — and branding was a
//  second endpoint of its own before ISettingProjector gave the join somewhere to live.
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ── The database ─────────────────────────────────────────────────────────────
// One PostgreSQL database holds the application's two tables and all six library tables.
// `UseMultiLingual(sp)` adds MultiLingual's EF Core interceptors, which is what makes a plain
// `db.Products.ToListAsync()` come back already translated into the request's language.
// It needs the (sp, options) overload of AddDbContext, so Aspire's connection string is read
// from configuration rather than through AddNpgsqlDbContext.
var connectionString = builder.Configuration.GetConnectionString("catalog")
    ?? throw new InvalidOperationException(
        "No 'catalog' connection string. Run the AppHost (aspire/DotNetBoostSample.AppHost), "
        + "which starts PostgreSQL and injects it.");

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseNpgsql(connectionString)
           .UseMultiLingual(sp));

// ── DotNetBoost.Settings ─────────────────────────────────────────────────────
// Runtime-editable configuration, stored in the Settings table of the same database.
//
// The library's own cache keys a group by its name, which is right while no value in a group
// varies by language — and none does. `Setting` declares nothing translatable, so MultiLingual's
// interceptor never sees a settings write, and branding's translated name is resolved by
// BrandingProjector, in Settings/BrandingSettings.cs, by projecting a translation into the
// RESPONSE, leaving the cached group untouched.
//
// This did carry a `.UseCustomCache<LanguageAwareSettingCache>()` that appended the request's
// language to every key and evicted across all of them. It was correct, and it guarded nothing:
// the one value that reads differently per language never reaches the cache. A group whose values
// genuinely do differ per language needs it back — git history has it, and the shape to copy is a
// language suffix on read plus eviction wider than the write.
builder.Services
    .AddSettings()
    .UseEntityFrameworkCore<AppDbContext>()
    // Scans this assembly for AbstractValidator<T>, so every write is checked — and registers an
    // ISettingSchemaContributor over those same rules, so GET /api/settings/{group}/schema
    // publishes them. Each group's rules sit beside the group — see
    // BrandingSettingsValidator in Settings/BrandingSettings.cs: the bounds the dashboard's form
    // draws its controls from now come from here instead of being hardcoded in the client.
    .UseFluentValidation(typeof(Program).Assembly)
    // And one of this application's own, publishing `translated` for a property carrying
    // [MultiLingual]. DotNetBoost.Settings cannot know that attribute exists — it belongs to
    // another library — so the schema endpoint takes it from here, and the dashboard's form stops
    // carrying the flag itself. See Settings/MultiLingualSchemaContributor.cs.
    //
    // Contributors MERGE rather than replace, so storeName comes back with FluentValidation's
    // `required` and `maxLength` as well as this one's flag. Order decides a collision — first
    // registered wins — and there is none here: the two describe different constraints.
    .UseSchemaContributor<MultiLingualSchemaContributor>()
    // And the read-path shape for one group: GET /api/settings/branding answers with its text in
    // the request's language and its logo, neither of which is a setting. Three libraries joined
    // in one class, on the group's own endpoint, so there is one URL for branding rather than two.
    // See BrandingProjector, in Settings/BrandingSettings.cs.
    .UseProjector<BrandingSettings, BrandingProjector>()
    .Build();

// ── DotNetBoost.Media ────────────────────────────────────────────────────────
// Rows in PostgreSQL, bytes on local disk under ./media. Switching to Azure or S3 is one line
// here and nothing else — including nothing in MediaText.cs.
// Overridable with Media__Root so a test run writes its bytes into its own temporary directory
// rather than into the repository's media folder. Unset, it is the ./media beside the solution,
// which is what `aspire run` uses and what the README documents.
var mediaRoot = builder.Configuration["Media:Root"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "..", "media");

builder.Services
    .AddMedia()
    .WithMaxFileSize(25 * 1024 * 1024)
    .AllowContentTypes("image/*", "application/pdf")
    .DeclareRoles("Product",  roles => roles.One("primary").Many("gallery"))
    .DeclareRoles("Category", roles => roles.One("banner"))
    // Branding declares its roles on the class instead, with [MediaRole] — so they sit next to
    // the thing that has them rather than in this file. The owner it files them under comes from
    // that class's [MediaEntity], and it has no table behind it at all: an owner is only ever two
    // strings, so the library does not care whether a row exists. That is what lets the dashboard
    // draw branding's media with the same component it draws a product's with.
    .DeclareRoles<BrandingSettings>()
    .UseEntityFrameworkCore<AppDbContext>()
    .UseFileSystem(Path.GetFullPath(mediaRoot), isDefault: true)
    // When a file goes, its per-language alt text goes with it. Registered once here rather than
    // remembered beside every delete — including the deletes this application does not make, like
    // DotNetBoost.Media's own DELETE /api/media/{id}, which used to strand those rows. The handler
    // is told the files that were actually deleted and not the ones merely detached, so an image
    // still in use somewhere else keeps its text. See Integration/MediaTextCleanup.cs.
    .OnFilesDeleted<MediaTextCleanup>()
    .Build();

// ── DotNetBoost.MultiLingual ─────────────────────────────────────────────────
// Translations and Languages tables in the same database. Languages are seeded in Data/Seed.cs.
builder.Services
    .AddMultiLingual()
    .UseEntityFrameworkCore<AppDbContext>()
    // The API's OWN messages, from Resources/{en,ar,fr}.json, through IStringLocalizer<ApiMessages>.
    //
    // Translated content is data in the Translations table; a message like "Category 3 was not
    // found." is code, so it lives in a file next to the code. What this adds over the framework's
    // AddLocalization() is which language it answers in: IMultiLingualManager.CurrentLanguage, the
    // one the EF interceptors just translated the row with — not CultureInfo.CurrentUICulture.
    // A refusal and the content it was about therefore cannot disagree.
    .AddJsonLocalization()
    // Which subjects' translations are reachable over HTTP. Opt-in on purpose: the routes take an
    // entity name in the path, so without a list they would be a write-anything surface over every
    // translatable thing in the database. An unlisted name is not routed at all.
    //
    //   MediaFile  — alt text and captions for an uploaded file. A type this application does not
    //                own, so it is named by string; MediaText.Subject is that one literal.
    //   Branding   — the settings group's translated StoreName, by type, so the name comes from
    //                its [MultiLingualEntity] rather than being written out again.
    //
    // Category and Product are deliberately NOT here. Their text is written as part of the product
    // or category it belongs to, through the typed accessor in CatalogEndpoints, where it is saved
    // in the same transaction as the SKU and the price. A second way in would be a second answer.
    .ExposeOverHttp(entities => entities
        .Allow(MediaText.Subject)
        .Allow<BrandingSettings>())
    .Build();

// ── The integration code in the solution ─────────────────────────────────────
builder.Services.AddScoped<MediaText>();

// Problem details for the refusals the application writes itself. MultiLingual's own refusals
// need nothing here: its endpoint filter turns them into problem details, and that mapping already
// looks the title and detail up through the localizer registered above.
builder.Services.AddProblemDetails();

// The OpenAPI document. Nothing to correct in it: BrandingProjector declares its response type
// through ISettingProjector<BrandingSettings, BrandingResponse>, so the generated GET publishes the
// projection rather than the stored group. This used to be nine lines and an operation transformer
// rewriting that one schema by route — the library takes the declaration now.
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseExceptionHandler();
app.UseCors();

// Creates the schema and seeds languages, categories, products and their translations.
// A real application would use EF Core migrations; a sample that must run with one command
// should not ask you to run two.
await app.Services.InitialiseDatabaseAsync();

// Resolves the request's language from Accept-Language, so every read below is translated.
// Must come before the endpoints that read translated entities.
app.UseMultiLingual();

app.MapOpenApi();
app.MapScalarApiReference();

// ── Each library maps its own endpoints. Four calls, no coordination. ───────
// All three tag their own routes, so the OpenAPI document groups them without help from here.
// This used to be app.MapGroup("").WithTags("Settings").MapSettingsEndpoints() — an empty prefix
// whose only job was to carry a tag in, because Settings was the one library that did not add its
// own. It does now.
app.MapSettingsEndpoints();                     // /api/settings, /api/settings/{group}[/schema]
app.MapMediaEndpoints();                        // /api/media
app.MapLanguageEndpoints();                     // /api/languages
app.MapTranslationEndpoints();                  // /api/translations/{entityName}/{entityId}

// ── The application's own endpoints ──────────────────────────────────────────
app.MapCatalogEndpoints();                      // /api/categories, /api/products

app.Run();

/// <summary>
/// Named so the integration tests can boot this exact application through
/// <c>WebApplicationFactory&lt;Program&gt;</c> — top-level statements otherwise compile to a class
/// no other assembly can see.
/// <para>
/// It carries no code on purpose. The tests run the real Program.cs above, with the real three
/// registrations, because a test that boots a different composition proves nothing about this one.
/// </para>
/// </summary>
public partial class Program;
