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
//  Settings, Media, MultiLingual and Auditing are installed side by side, each configured on its
//  own terms in Infrastructure/DependencyInjection.cs. There is no AddDotNetBoost(), no shared
//  kernel and no integration package, and tests/ZeroCouplingTests.cs fails the build if that
//  changes.
//
//  What joins them is nine files of this application's own code. The README's "Where the
//  composition happens" says what each one does and why:
//
//    Infrastructure/Persistence/AppDbContext.cs                — all four: one Apply* call each
//    Infrastructure/DependencyInjection.cs                     — .IgnoreEntity<Translation>()
//    Infrastructure/Persistence/SettingGroupAuditObserver.cs   — one audit entry per settings save
//    Application/Composition/MediaText.cs                      — a file's per-language alt text
//    Application/Composition/MediaTextCleanup.cs               — deleted with the file
//    Application/Composition/MultiLingualSchemaContributor.cs  — `translated` in settings schemas
//    Application/Settings/BrandingSettings.cs                  — branding's text and logo, joined
//    Domain/Catalog/{Category,Product}.cs                      — [AuditIgnore] beside [MultiLingual]
//
//  This file is the host and nothing else: it reads configuration and says in what order the
//  pieces go together. The catalogue's routes are slices under Application/Catalog, one file each.
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ── Configuration, read by the host ──────────────────────────────────────────
// Resolved here and passed in, so the infrastructure layer depends on neither
// Microsoft.Extensions.Configuration nor Hosting, and so this failure can point at the AppHost.
var connectionString = builder.Configuration.GetConnectionString("catalog")
    ?? throw new InvalidOperationException(
        "No 'catalog' connection string. Run the AppHost (aspire/DotNetBoostSample.AppHost), "
        + "which starts PostgreSQL and injects it.");

// Media__Root lets a test run keep its bytes in a temporary directory. Unset, it is ./media
// beside the solution.
var mediaRoot = builder.Configuration["Media:Root"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "..", "media");

// MultiLingualOptions.BaseLanguage: which language, if any, lives in the entities' own columns
// rather than in translation rows. Empty by default, so every language is a row. Set it to "en" to
// run the other way — against a fresh database, because it decides whether Products has a Name
// column at all. No endpoint's response and no line of the dashboard changes either way.
var baseLanguage = builder.Configuration["MultiLingual:BaseLanguage"];

// ── The database and all four libraries ──────────────────────────────────────
// The last two arguments are configured here rather than in AddInfrastructure because their
// methods live in the *.API packages the host installs: they are facts about HTTP, not storage.
//
// ExposeOverHttp is opt-in because the translation routes take an entity name in the path; without
// a list they would be a write-anything surface. Category and Product are left off on purpose:
// their text is saved with the row in the Create and Update slices, and a second way in would be a
// second answer.
//
// UseHttpContextActor puts the signed-in user on every entry. UseLoggingObserver logs what the
// engine chose not to record, so a skipped change can be told apart from one nobody made.
builder.Services.AddInfrastructure(
    connectionString,
    Path.GetFullPath(mediaRoot),
    baseLanguage,
    multiLingual => multiLingual.ExposeOverHttp(entities => entities
        .Allow(MediaText.Subject)          // alt text and captions for an uploaded file
        .Allow<BrandingSettings>()),       // the settings group's translated StoreName
    audit => audit
        .UseHttpContextActor()
        .UseLoggingObserver());

// ── The dispatcher, the eleven slices, the behaviors, the composition ────────
builder.Services.AddApplication();

// Problem details for the refusals the application writes itself. ASP.NET does not map
// ValidationBehavior's ValidationException by itself — without ValidationExceptionHandler an empty
// SKU is a 500 — and DbUpdateExceptionHandler makes a duplicate slug or SKU a 409. Each declines
// what is not its own, so their order only decides who is asked first.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<DbUpdateExceptionHandler>();

// BrandingProjector declares its response type, so the generated GET publishes the projection.
// The catalogue slices declare theirs with .Produces<T>(), because an endpoint returning IResult
// describes its 200 as "OK" and nothing more.
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ── Who is asking ───────────────────────────────────────────────────────────
// A header-trusting stand-in for authentication. It verifies nothing — read
// Authentication/SampleActorAuthentication.cs before copying a line of it. It exists because
// DotNetBoost.Auditing's endpoints require a caller and an entry's actor is HttpContext.User, and
// it is the default scheme so that catalogue writes are attributed too, not only trail reads.
builder.Services
    .AddAuthentication(SampleActorAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SampleActorAuthenticationHandler>(
        SampleActorAuthenticationHandler.SchemeName, configureOptions: null);

builder.Services.AddAuthorization();

// ── Retention ───────────────────────────────────────────────────────────────
// Prunes audit entries past the age the `auditing` settings group states, while that group says to.
// A timer is a fact about the process, so the host registers it. See AuditRetentionService for why
// it prunes in-process rather than through POST /api/audit/prune.
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddHostedService<AuditRetentionService>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseExceptionHandler();
app.UseCors();

// Before the endpoints, so HttpContext.User is set by the time a handler saves anything.
app.UseAuthentication();
app.UseAuthorization();

// Creates the schema and seeds languages, categories, products and their translations.
// A real application would use EF Core migrations; a sample that must run with one command
// should not ask you to run two.
await app.Services.InitialiseDatabaseAsync();

// Resolves the request's language from Accept-Language. Must come before the endpoints that read
// translated entities.
app.UseMultiLingual();

app.MapOpenApi();
app.MapScalarApiReference();

// ── Each library maps its own endpoints. Five calls, no coordination. ───────
app.MapSettingsEndpoints();                     // /api/settings, /api/settings/{group}[/schema]
app.MapMediaEndpoints();                        // /api/media
app.MapLanguageEndpoints();                     // /api/languages
app.MapTranslationEndpoints();                  // /api/translations/{entityName}/{entityId}

// /api/audit, /api/audit/{id} and the two discovery routes, with the library's defaults: the reads
// require an authenticated caller, and the erasure and prune endpoints are not mapped. Both destroy
// history, and nothing in this sample should be able to do that over HTTP.
app.MapAuditEndpoints();

// ── The application's own endpoints ──────────────────────────────────────────
// Every IEndpoint in the Application assembly: /api/categories and /api/products.
app.MapApplicationEndpoints();

app.Run();

/// <summary>
/// Named so the integration tests can boot this exact application through
/// <c>WebApplicationFactory&lt;Program&gt;</c> — top-level statements otherwise compile to a class
/// no other assembly can see.
/// </summary>
public partial class Program;
