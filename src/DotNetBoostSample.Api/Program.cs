using DotNetBoost.Auditing.API;
using DotNetBoost.Media.API;
using DotNetBoost.MultiLingual.API;
using DotNetBoostSample.Api.Auditing;
using DotNetBoostSample.Api.Authentication;
using DotNetBoostSample.Application;
using DotNetBoostSample.Application.Abstractions.Cqrs;
using DotNetBoostSample.Application.Abstractions.Endpoints;
using DotNetBoostSample.Application.Abstractions.Persistence;
using DotNetBoostSample.Application.Composition;
using DotNetBoostSample.Application.Settings;
using DotNetBoostSample.Infrastructure;
using DotNetBoostSample.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scalar.AspNetCore;

// ─────────────────────────────────────────────────────────────────────────────
//  DotNetBoost sample — four independent libraries in one application.
//
//  Settings, Media, MultiLingual and Auditing are installed side by side. Each is configured on
//  its own terms, in its own block. There is no AddDotNetBoost(), no shared kernel package, and no
//  integration package pairing any two of them: see Directory.Packages.props for the full list of
//  what is installed, and tests/ for the assertion that none of them references another.
//
//  The fourth arrived without moving the other three. Auditing's whole presence in this file is
//  one delegate, one Map call and — added later, for retention — one hosted service; in the rest
//  of the solution it is one Apply* line in AppDbContext, one block and one UseAuditing(sp) in AddInfrastructure, one settings observer
//  beside them, and two attributes in Domain — no handler mentions it. Nothing in the other three moved. That is the strongest
//  evidence here for what "no shared kernel" is worth, because it is the only one measured by
//  ADDING a library rather than by reading the ones already in — and the last two items on that
//  list are also where the cost showed up, which is the rest of this comment.
//
//  What ties them together is nine files, all of them application and domain code, none of them
//  large, and each one a place where this application says two libraries are talking about the
//  same thing — except the last three, which are where it says one of them must leave the other's
//  data alone, or take it in another shape:
//
//    • Infrastructure/Persistence/AppDbContext.cs     — all four. One database, four Apply*
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
//    • Application/Settings/BrandingSettings.cs       — three of the four, in BrandingProjector.
//                                        Answers one settings group with its text in the request's
//                                        language and its logo.
//
//  Auditing added three more, and they are the first on this list that exist because two libraries
//  DISAGREE rather than because they are talking about the same thing:
//
//    • Infrastructure/DependencyInjection.cs          — Auditing → MultiLingual, one line of it:
//                                        .IgnoreEntity<Translation>(). MultiLingual's row is keyed
//                                        on four columns and an audit entry on one, and nobody
//                                        here owns the type to put [AuditIgnore] on it — so the
//                                        attribute is applied from the outside. Until
//                                        DotNetBoost.Auditing 1.0.0-preview.2 there was no way to,
//                                        the capture could not be attached to this context at all,
//                                        and the catalogue's handlers recorded every change by hand.
//    • Domain/Catalog/{Category,Product}.cs           — MultiLingual → Auditing. [AuditIgnore]
//                                        beside [MultiLingual] on the two translated properties: a
//                                        trail has no language, so a property whose value depends
//                                        on one cannot be stated in it. One sentence, said twice,
//                                        which is why nine files are eight entries.
//    • Infrastructure/Persistence/SettingGroupAuditObserver.cs — Settings → Auditing. Settings
//                                        stores a group one row per property and the trail wants
//                                        the save, so the rows are ignored (.IgnoreEntity<Setting>())
//                                        and this ISettingWriteObserver records each save as one
//                                        entry for the group — after the save commits, not with it.
//
//  Nine, counted the same way as five was. The README said then that a sixth would mean the claim
//  had quietly stopped being true, and it has: three of these exist because the libraries disagree
//  about what can be recorded, which is a worse kind of glue than "these two tables are about one
//  thing" and is worth saying out loud rather than absorbing into the count.
//
//  Six of the nine turn out to be abstraction-only — they name DotNetBoost.*.Core and nothing
//  else — which is why they sit in the application and domain layers beside the code that uses
//  them. The other three — AppDbContext, that one line of AddInfrastructure and the settings
//  observer, which saves its entry through AppDbContext — are infrastructure, and all three are
//  about sharing one DbContext. Joining two libraries is not a persistence concern;
//  joining them in ONE DATABASE is.
//
//  The count is eight rather than a rounder number because every file joining two libraries is on
//  the list, including the two that are there because two libraries disagree.
//
//  It was more. Writing alt text is not here any more — DotNetBoost.MultiLingual ships the
//  translation endpoints now, and the dashboard's editor calls them directly — and branding was a
//  second endpoint of its own before ISettingProjector gave the join somewhere to live.
//
//  ── And what this file is now ───────────────────────────────────────────────
//  Composition, and nothing else. The ── blocks that used to configure the libraries are in
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

// ── The database and all four libraries ──────────────────────────────────────
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
//
// The fourth argument is the audit engine's two host-shaped decisions, and it is here for exactly
// the reason the third one is: both methods live in a *.API package, because both are facts about
// an HTTP request.
//
//   UseHttpContextActor — the signed-in user of the current request is the actor on every entry
//                         the slices record during it. Without it the trail records "unknown" for
//                         all of them, which is honest and nearly useless. See
//                         Authentication/SampleActorAuthentication.cs for what signs in here, and
//                         for why a sample with no authentication grew some.
//   UseLoggingObserver  — writes what the engine decided NOT to record to ILogger. A change that
//                         was deliberately skipped is otherwise indistinguishable from one nobody
//                         made, and "the trail is silent about this entity" is a question someone
//                         asks in production, not in a code review.
builder.Services.AddInfrastructure(
    connectionString,
    Path.GetFullPath(mediaRoot),
    baseLanguage,
    multiLingual => multiLingual.ExposeOverHttp(entities => entities
        .Allow(MediaText.Subject)
        .Allow<BrandingSettings>()),
    audit => audit
        .UseHttpContextActor()
        .UseLoggingObserver());

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

// ── Who is asking ───────────────────────────────────────────────────────────
// A header-trusting stand-in for authentication, and the only reason this sample has any: the
// audit endpoints require an authenticated caller because DotNetBoost.Auditing maps them that way,
// and the actor on an entry is whoever HttpContext.User says it is. Read
// Authentication/SampleActorAuthentication.cs before copying a line of this; it verifies nothing.
//
// Nothing else on this API requires a caller. The scheme is registered as the default so that a
// request carrying the header is authenticated everywhere — which is what gets a name onto the
// entries a catalogue write produces, not just onto the reads of the trail.
builder.Services
    .AddAuthentication(SampleActorAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SampleActorAuthenticationHandler>(
        SampleActorAuthenticationHandler.SchemeName, configureOptions: null);

builder.Services.AddAuthorization();

// ── Retention ───────────────────────────────────────────────────────────────
// Deletes audit entries past the age the `auditing` settings group states, while that group says
// to — off until someone turns it on. A timer is a fact about the process, so it is registered by
// the host rather than in AddInfrastructure. See Auditing/AuditRetentionService.cs for why it
// reads the group every minute, and why it prunes in-process rather than through POST
// /api/audit/prune, which stays unmapped below.
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddHostedService<AuditRetentionService>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseExceptionHandler();
app.UseCors();

// Before the endpoints, so HttpContext.User is populated by the time a handler saves anything —
// the actor is read while the change is being recorded, not afterwards.
app.UseAuthentication();
app.UseAuthorization();

// Creates the schema and seeds languages, categories, products and their translations.
// A real application would use EF Core migrations; a sample that must run with one command
// should not ask you to run two.
await app.Services.InitialiseDatabaseAsync();

// Resolves the request's language from Accept-Language, so every read below is translated.
// Must come before the endpoints that read translated entities.
app.UseMultiLingual();

app.MapOpenApi();
app.MapScalarApiReference();

// ── Each library maps its own endpoints. Five calls, no coordination. ───────
// All four tag their own routes, so the OpenAPI document groups them without help from here.
// None of this is touched by the slice architecture: these are the libraries' endpoints, they are
// not slices, and they never will be.
app.MapSettingsEndpoints();                     // /api/settings, /api/settings/{group}[/schema]
app.MapMediaEndpoints();                        // /api/media
app.MapLanguageEndpoints();                     // /api/languages
app.MapTranslationEndpoints();                  // /api/translations/{entityName}/{entityId}

// /api/audit, /api/audit/{id}, and the two discovery routes. Called bare on purpose:
//
//   • The reads REQUIRE AN AUTHENTICATED CALLER, because that is this library's default and the
//     one it argues for. Passing o.RequireAuthorization = false would make the trail of this
//     sample's every change public, and the library's own quick start says not to copy that line.
//   • The erasure and prune endpoints are NOT MAPPED — o.MapErasure and o.MapPrune are off by
//     default, so DELETE /api/audit/entity/… and POST /api/audit/prune do not exist here. They
//     destroy history. Nothing in a sample needs to, and an endpoint that does should be asked for
//     deliberately rather than inherited. Retention is not a reason to map prune: the policy is
//     the `auditing` settings group, and AuditRetentionService carries it out in-process, so no
//     HTTP caller can delete history on demand.
app.MapAuditEndpoints();

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
/// It carries no code on purpose. The tests run the real Program.cs above, with the real four
/// registrations, because a test that boots a different composition proves nothing about this one.
/// </para>
/// </summary>
public partial class Program;
