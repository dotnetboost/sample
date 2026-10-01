# DotNetBoost Sample

**Three DotNetBoost libraries in one application, with zero references between them.**

This sample installs [DotNetBoost.Settings](https://www.nuget.org/packages/DotNetBoost.Settings.Core),
[DotNetBoost.Media](https://www.nuget.org/packages/DotNetBoost.Media.Core) and
[DotNetBoost.MultiLingual](https://www.nuget.org/packages/DotNetBoost.MultiLingual.Core) from
nuget.org and composes them into a small product catalogue — a store with categories and products
whose names are translated, product images with per-language alt text, and branding you can change
at runtime.

What it is built to demonstrate is as much what is *absent* as what is present:

- no `DotNetBoost.Core` or `DotNetBoost.Abstractions` shared-kernel package
- no `AddDotNetBoost(b => b.AddSettings().AddMedia())` unifying builder
- no integration package pairing any two libraries

Each library is installed on its own and configured on its own terms. Everything that joins them
is this application's code, in two files. `tests/` asserts that none of them references another,
so the claim cannot quietly stop being true.

---

## The dashboard

A sidebar shell matching the three libraries' own dashboards — **Catalog**, **Media**,
**Languages**, **Settings**, with the reading-language selector in every page header.

**Catalog** has two tabs. Each lists, edits and deletes, and each editor is the composition on one
form:

- a **button per available language**, ticked when that language has content, with the fields for
  whichever is selected — right-aligned automatically for Arabic
- a **media picker box** choosing from the library: a banner for a category, a primary image and a
  gallery for a product

Nothing on those forms knows it is touching three libraries. The slug and SKU are columns; the
name and description are translation rows; the images are attachment rows.

## Run it

Needs **.NET 10 SDK**, **Node 20+**, and a **container runtime** (Docker Desktop, OrbStack or
Podman) for PostgreSQL.

```bash
aspire run
```

Or, without the Aspire CLI:

```bash
dotnet run --project aspire/DotNetBoostSample.AppHost
```

The Aspire dashboard opens with three resources: `postgres`, `api` and `dashboard`. Open the
`dashboard` URL. The database schema is created and seeded on first run — three languages
(English, Arabic, French), two categories and three products, each translated.

Switch the language in the dashboard header. Every product name, description and image alt text
is re-read in that language, and Arabic flips the shell to RTL.

---

## Where the composition happens

### 1. One database, three libraries — `src/DotNetBoostSample.Api/Data/AppDbContext.cs`

The only place all three libraries are named together, and all it amounts to is implementing three
independent interfaces and calling three independent `Apply*` methods:

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), ISettingDbContext, IMediaDbContext, IMultiLingualDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ... the application's own Categories and Products

        modelBuilder.ApplySettingsConfiguration(DatabaseProvider.PostgreSql);
        modelBuilder.ApplyMedia();
        modelBuilder.ApplyMultiLingual(this);
    }
}
```

Remove a library and you delete its interface, its `DbSet`s, and its one line. Nothing else
changes.

### 2. Per-language alt text — `src/DotNetBoostSample.Api/Integration/MediaText.cs`

The interesting one, because `MediaFile` has **no** alt-text or caption field, and alt text is both
required for accessibility and inherently per-language.

Both libraries address a subject by the same two strings — an entity name and an entity id.
MultiLingual stores translations against `(EntityName, EntityId, PropertyName, LanguageCode)`;
Media exposes `IMediaManager.For(string entityName, string entityId)` for callers holding the pair
rather than an object. **That shared addressing convention is the entire integration surface.**

So the glue declares one subject name and uses a media file's own `Guid` as the entity id:

```csharp
public const string Subject = "MediaFile";

private static string Key(Guid mediaFileId) => mediaFileId.ToString("D");

public Task SetAsync(Guid mediaFileId, string languageCode, string propertyName, string? value, …) =>
    translations.UpsertManyAsync([new Translation
    {
        EntityName   = Subject,
        EntityId     = Key(mediaFileId),
        PropertyName = propertyName,     // "AltText" or "Caption"
        LanguageCode = languageCode,
        Value        = value             // null removes the row
    }], ct);
```

No new table, no schema change in either library, and swapping media storage from local disk to
Azure or S3 does not touch this file.

### 3. A setting whose value is a file — `src/DotNetBoostSample.Api/Settings/BrandingSettings.cs`

Settings and Media composed with no mechanism at all:

```csharp
[SettingGroup("branding")]
public class BrandingSettings
{
    public Guid LogoFileId { get; set; }
    // ...
}

var branding = await settings.For<BrandingSettings>().GetAsync();
var logo     = await media.FindAsync(branding.LogoFileId);
```

The settings library stores a `Guid` as it would store any value. Only the application knows it
means a file.

---

## What this costs

Being honest about the trade-off, because it is the point of the sample:

| | |
|---|---|
| **Glue per app** | Every application writes `MediaText.cs` itself — about 100 lines here, most of it comment. |
| **No cascade** | Deleting a `MediaFile` does not delete its alt text; the media library has no idea it exists. `DELETE /api/products/{id}` writes that cleanup out by hand. |
| **Magic strings** | `"MediaFile"` has no compile-time link to the type. Mitigated with one `const`. |
| **No shared guarantee** | Both libraries must format keys identically. They do — but by a hand-maintained convention, which is why `tests/KeyFormatAgreementTests.cs` asserts it rather than trusting it. |

None of these are capability limits. They are ergonomics, and they are the price of being able to
install one library without the other two.

---

## Tests

```bash
dotnet test
```

- **`ZeroCouplingTests`** — no library references another, and none references a
  `DotNetBoost.Core` or `DotNetBoost.Abstractions`. A README sentence decays; a failing build does
  not.
- **`KeyFormatAgreementTests`** — Media and MultiLingual format the same `Guid`, `int`, `long` and
  `string` key to the same string, and cap it at the same length. This is what makes
  `MediaText.cs` safe.

---

## Layout

```
src/DotNetBoostSample.Api/
  Data/AppDbContext.cs          one database, three Apply* calls
  Data/Seed.cs                  schema + languages + catalogue on first run
  Entities/                     Category, Product — the app's own two entities
  Settings/BrandingSettings.cs  Settings × Media
  Integration/MediaText.cs      Media × MultiLingual  ← the composition
  Endpoints/                    catalogue, media-text, branding
  Program.cs                    three independent registrations

aspire/DotNetBoostSample.AppHost/        postgres + api + dashboard
aspire/DotNetBoostSample.ServiceDefaults/ telemetry, health, discovery
clients/dashboard/                       Nuxt 4 + Nuxt UI, sidebar shell, RTL-aware
  app/pages/catalog.vue                  Categories | Products tabs — list, edit, delete
  app/components/CategoryEditor.vue      slug + per-language text + banner picker
  app/components/ProductEditor.vue       columns + per-language text + primary & gallery
  app/components/TranslationEditor.vue   a button per language, fields for the selected one
  app/components/MediaPicker.vue         pick an existing file from the library, or upload
tests/DotNetBoostSample.Tests/           the claim, as tests
media/                                   file bytes at runtime (gitignored)
```

---

## Endpoints

Each library maps its own, with no coordination between them:

| From | Route |
|---|---|
| `DotNetBoost.Settings.API` | `GET/POST /api/settings/{group}` |
| `DotNetBoost.Media.API` | `/api/media`, `/api/media/{id}/content`, `/api/media/{id}/url` |
| `DotNetBoost.MultiLingual.API` | `/api/languages` |
| This application | `/api/categories`, `/api/products`, `/api/media-text/{id}`, `/api/media-text/resolve`, `/api/branding` |

Full reference at `/scalar` on the API.

> **Two gaps this sample surfaced.**
>
> `DotNetBoost.MultiLingual.API` ships `MapLanguageEndpoints` but no translation-editing
> endpoints, so writing translations over HTTP is the application's job —
> `Endpoints/MediaTextEndpoints.cs` and `Endpoints/CatalogEndpoints.cs` do it.
>
> `DotNetBoost.Media.API`'s list endpoint returns `url` as a *signed, expiring* link, which is
> null on the FileSystem backend. The dashboard therefore derives every URL from the file's id
> (`app/utils/media.ts`) rather than reading the field — which also means switching to Azure or S3
> changes no client code.

---

## Configuration

`MEDIA` bytes go to `./media` on local disk, which needs no container and no credentials. To switch
to Azure Blob Storage or S3, change one line in `Program.cs`:

```csharp
.UseFileSystem(mediaRoot, isDefault: true)     // this sample
// .UseAzureBlobStorage(connectionString, "media", isDefault: true)
// .UseAmazonS3(…)
```

Files uploaded before the switch keep working, because every row records which backend wrote it.

## License

MIT.
