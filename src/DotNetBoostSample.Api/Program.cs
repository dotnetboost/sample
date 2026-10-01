using DotNetBoost.Media.API;
using DotNetBoost.MultiLingual.API;
using DotNetBoostSample.Api.Data;
using DotNetBoostSample.Api.Endpoints;
using DotNetBoostSample.Api.Integration;
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
//  What ties them together is in exactly two places, both of them application code:
//    • Data/AppDbContext.cs      — one database, three Apply* calls
//    • Integration/MediaText.cs  — per-language alt text, via the (entityName, entityId) convention
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
builder.Services
    .AddSettings()
    .UseEntityFrameworkCore<AppDbContext>()
    .Build();

// ── DotNetBoost.Media ────────────────────────────────────────────────────────
// Rows in PostgreSQL, bytes on local disk under ./media. Switching to Azure or S3 is one line
// here and nothing else — including nothing in MediaText.cs.
var mediaRoot = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "media");

builder.Services
    .AddMedia()
    .WithMaxFileSize(25 * 1024 * 1024)
    .AllowContentTypes("image/*", "application/pdf")
    .DeclareRoles("Product",  roles => roles.One("primary").Many("gallery"))
    .DeclareRoles("Category", roles => roles.One("banner"))
    .UseEntityFrameworkCore<AppDbContext>()
    .UseFileSystem(Path.GetFullPath(mediaRoot), isDefault: true)
    .Build();

// ── DotNetBoost.MultiLingual ─────────────────────────────────────────────────
// Translations and Languages tables in the same database. Languages are seeded in Data/Seed.cs.
builder.Services
    .AddMultiLingual()
    .UseEntityFrameworkCore<AppDbContext>()
    .Build();

// ── The only integration code in the solution ────────────────────────────────
builder.Services.AddScoped<MediaText>();

builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.MapDefaultEndpoints();
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

// ── Each library maps its own endpoints. Three calls, no coordination. ───────
app.MapSettingsEndpoints();                     // /api/settings/{group}
app.MapMediaEndpoints();                        // /api/media
app.MapLanguageEndpoints();                     // /api/languages

// ── The application's own endpoints ──────────────────────────────────────────
app.MapCatalogEndpoints();                      // /api/categories, /api/products
app.MapMediaTextEndpoints();                    // /api/media-text  — the composition
app.MapBrandingEndpoints();                     // /api/branding    — a setting holding a file id

app.Run();
