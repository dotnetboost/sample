using DotNetBoost.Media.API;
using DotNetBoost.MultiLingual.API;
using DotNetBoostSample.Application;
using DotNetBoostSample.Application.Abstractions.Cqrs;
using DotNetBoostSample.Application.Abstractions.Endpoints;
using DotNetBoostSample.Application.Abstractions.Persistence;
using DotNetBoostSample.Application.Composition;
using DotNetBoostSample.Application.Settings;
using DotNetBoostSample.Infrastructure;
using DotNetBoostSample.Infrastructure.Persistence;
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
//    • Infrastructure/Persistence/AppDbContext.cs     — all three. One database, three Apply*
//                                        calls, nothing else.
//    • Application/Composition/MediaText.cs           — Media × MultiLingual. Reads a file's
//                                        per-language alt text for this API's own responses, via
//                                        the (entityName, entityId) convention both libraries
//                                        address a subject by.
//    • Application/Composition/MediaTextCleanup.cs    — Media → MultiLingual. Removes that text
//                                        when Media says the file is gone.
//    • Application/Composition/MultiLingualSchemaContributor.cs — Settings × MultiLingual.
//                                        Publishes `translated` on a settings property carrying
//                                        [MultiLingual], which the settings library cannot know
//                                        about.
//    • Application/Settings/BrandingSettings.cs       — all three, in BrandingProjector. Answers
//                                        one settings group with its text in the request's
//                                        language and its logo.
//
//  Four of those five turn out to be abstraction-only — they name DotNetBoost.*.Core and nothing
//  else — which is why they sit in the application layer beside the slices that use them, in a
//  folder called Composition. Only AppDbContext is infrastructure. Joining two libraries is not a
//  persistence concern; joining them in ONE DATABASE is.
//
//  The count is five rather than a rounder number because every file joining two libraries is on
//  the list; a sixth would mean the claim had quietly stopped being true.
//
//  It was more. Writing alt text is not here any more — DotNetBoost.MultiLingual ships the
//  translation endpoints now, and the dashboard's editor calls them directly — and branding was a
//  second endpoint of its own before ISettingProjector gave the join somewhere to live.
//
//  ── And what this file is now ───────────────────────────────────────────────
//  Composition, and nothing else. The four ── blocks that used to configure the libraries are in
//  Infrastructure/DependencyInjection.cs, moved whole; the catalogue's eleven routes are eleven
//  slices under Application/Catalog, one file each, holding their own query, response, bounds,
//  handler and URL. What is left here is reading configuration, which is the host's job, and
//  saying in what order the pieces go together.
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ── Configuration, read by the host ──────────────────────────────────────────
// Both values are resolved here and passed in, so the infrastructure layer depends on neither
// Microsoft.Extensions.Configuration nor Hosting — and so this failure can say something useful
// about the AppHost, which is the only thing that knows how to fix it.
var connectionString = builder.Configuration.GetConnectionString("catalog")
    ?? throw new InvalidOperationException(
        "No 'catalog' connection string. Run the AppHost (aspire/DotNetBoostSample.AppHost), "
        + "which starts PostgreSQL and injects it.");

// Overridable with Media__Root so a test run writes its bytes into its own temporary directory
// rather than into the repository's media folder. Unset, it is the ./media beside the solution,
// which is what `aspire run` uses and what the README documents.
var mediaRoot = builder.Configuration["Media:Root"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "..", "media");

// Which language, if any, lives in the entities' own columns rather than in translation rows —
// MultiLingualOptions.BaseLanguage. Empty in appsettings.json, so the sample runs in the mode the
// README describes: no column for a [MultiLingual] property, every language a row.
//
// Set it to "en" — here, with MultiLingual__BaseLanguage, or with the one line in
// appsettings.json — and the sample runs in base-language mode instead, against a fresh database.
// Nothing else in the solution changes: not an endpoint's response, not a line of the dashboard.
// The four catalogue writes and the two catalogue detail reads ask the library which mode it is
// in and do the right thing; everything else was already right in both.
//
// A fresh database, because this is a SCHEMA decision — it decides whether Products has a Name
// column — so flipping it against existing content would leave that content half in rows and
// half in a column that was never filled.
var baseLanguage = builder.Configuration["MultiLingual:BaseLanguage"];

// ── The database and all three libraries ─────────────────────────────────────
// The third argument is this application's HTTP surface over translations, and it is here rather
// than in AddInfrastructure because ExposeOverHttp lives in DotNetBoost.MultiLingual.API — the
// package the HOST installs. Which translations are reachable is a routing decision; where they
// are stored is the infrastructure layer's.
//
// Opt-in on purpose: the routes take an entity name in the path, so without a list they would be a
// write-anything surface over every translatable thing in the database. An unlisted name is not
// routed at all.
//
//   MediaFile  — alt text and captions for an uploaded file. A type this application does not own,
//                so it is named by string; MediaText.Subject is that one literal.
//   Branding   — the settings group's translated StoreName, by type, so the name comes from its
//                [MultiLingualEntity] rather than being written out again.
//
// Category and Product are deliberately NOT here. Their text is written as part of the product or
// category it belongs to, through the typed accessor in the Create and Update slices, where it is
// saved in the same transaction as the SKU and the price. A second way in would be a second answer.
builder.Services.AddInfrastructure(
    connectionString,
    Path.GetFullPath(mediaRoot),
    baseLanguage,
    multiLingual => multiLingual.ExposeOverHttp(entities => entities
        .Allow(MediaText.Subject)
        .Allow<BrandingSettings>()));

// ── The dispatcher, the eleven slices, the behaviors, the composition ────────
builder.Services.AddApplication();

// Problem details for the refusals the application writes itself. ResultExtensions turns an Error
// into one; ValidationExceptionHandler does the same for the ValidationException that
// ValidationBehavior throws, which ASP.NET does NOT map by itself — without it an empty SKU is a
// 500. MultiLingual's own refusals need nothing here: its endpoint filter turns them into problem
// details, and that mapping already looks the title and detail up through the localizer registered
// in AddInfrastructure.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
// And the constraints the database refuses: a duplicate slug or SKU is a 409, not a 500. Each
// handler declines what is not its own, so the order of these two only decides who is asked first.
builder.Services.AddExceptionHandler<DbUpdateExceptionHandler>();

// The OpenAPI document. Nothing to correct in it: BrandingProjector declares its response type
// through ISettingProjector<BrandingSettings, BrandingResponse>, so the generated GET publishes the
// projection rather than the stored group. This used to be nine lines and an operation transformer
// rewriting one schema by route.
//
// The catalogue's own shapes are published because each slice SAYS so, which is a thing naming a
// record does not do by itself: an endpoint returning IResult describes its 200 as "OK" and
// nothing more. The two listings return their value directly and are inferred; the nine that
// answer 200-or-404 carry .Produces<T>() and .ProducesProblem(). Worth knowing, because a comment
// here claimed the records were enough until the document was actually read.
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
// None of this is touched by the slice architecture: these are the libraries' endpoints, they are
// not slices, and they never will be.
app.MapSettingsEndpoints();                     // /api/settings, /api/settings/{group}[/schema]
app.MapMediaEndpoints();                        // /api/media
app.MapLanguageEndpoints();                     // /api/languages
app.MapTranslationEndpoints();                  // /api/translations/{entityName}/{entityId}

// ── The application's own endpoints ──────────────────────────────────────────
// Every IEndpoint in the Application assembly: /api/categories and /api/products, eleven routes
// from eleven slices, each one declared in the file that answers it.
app.MapApplicationEndpoints();

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
