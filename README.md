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

**Media** is DotNetBoost.Media's own browser, lifted wholesale — search, category and tag filters,
content-type filter, sorting, paging, upload, file details, attachments and delete, all of it in
the query the API is asked rather than applied to the rows already on screen. The one addition is
**alt text**, which appears in a file's details below "Used by": it is not media data at all, but
DotNetBoost.MultiLingual rows filed under the subject `MediaFile`.

**Catalog** has two tabs. Each lists, edits and deletes, and each editor is the composition on one
form:

- a **button per available language**, ticked when that language has content, with the fields for
  whichever is selected — right-aligned automatically for Arabic
- **a box per media role**, read from `GET /api/media/roles/{entityName}`: one box for a category's
  `banner`, one for a product's `primary`, and a box per file plus a trailing empty one for its
  `gallery`. Clicking a box opens the same browser in a modal, already committed to that role.

Nothing on those forms knows it is touching three libraries. The slug and SKU are columns; the
name and description are translation rows; the images are attachment rows — and attaching,
detaching and reordering go straight to **the media library's own endpoints**, so this application
has no attachment endpoints of its own.

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

### 3. Branding: text from one library, its logo from another — `src/.../Settings/BrandingSettings.cs`

The branding settings group holds text. Its logo is a **media attachment**, on an owner that has no
table anywhere:

```csharp
[SettingGroup("branding")]
[MediaEntity("Branding")]          // pins the stored owner name against a class rename
[MediaRole("logo")]                // declared on the class, not in Program.cs
public class BrandingSettings
{
    public const string MediaOwnerName = "Branding";
    public const string MediaOwnerId   = "branding";
    public const string LogoRole       = "logo";

    public string StoreName   { get; set; } = "DotNetBoost Sample Store";
    public string AccentColor { get; set; } = "#512BD4";
}
```

```csharp
// Program.cs — reads the [MediaRole] attributes off that one class.
.DeclareRoles<BrandingSettings>()

// Reading it back.
var branding = await settings.For<BrandingSettings>().GetAsync();
var logo     = await media.For("Branding", "branding").GetByRoleAsync("logo");
```

**This is the sharpest demonstration of the attachment model in the sample.** Products show
attaching to rows, which any schema can do with a foreign key. Branding has no row at all — it is a
group of setting rows and there can only ever be one of it — and a foreign key could not express
that. The media library does not care: an owner is two opaque strings it never tries to resolve. So
the dashboard draws branding's logo with the *same* role box and the *same* picker it draws a
product's primary image with, over the same `GET /api/media/roles/{entityName}`.

The id `"branding"` is degenerate — there is only one branding, so it carries no information, the
way a config table with a single row conventionally uses `Id = 1`. It has to be *something*,
because the library requires both halves of an address, and that uniformity is exactly what lets
one table serve every owner with no column per owner type.

#### Why the logo is not a `Guid` on the settings class

It was, and that is a perfectly ordinary way to reference a file. Two problems surfaced, in order:

1. A file referenced only by a setting's value is **invisible to the media library** — it appears in
   no entity's attachments, its details panel says it is used by nothing, and deleting it warns
   about nothing, right up until the site loses its logo.
2. Attaching it *as well* fixed the reading but left **two records of one fact**. A settings change
   handler kept them in step one way; nothing could keep them in step the other way, because
   `DotNetBoost.Media` ships no change hook at all. Detaching the logo from the Media page removed
   the attachment and left the setting pointing at the file.

So the attachment is now the only record. Nothing can drift, because detaching the logo and the
site having no logo are the same statement.

**What that costs, honestly.** The logo is no longer part of the settings group, so it is absent
from `GET /api/settings/branding`, from that group's ETag, and from its audit trail. Those three
things are what storing an id in a setting buys, and they were given up deliberately for a single
source of truth. If you need a file reference inside a settings group's versioning and audit, keep
the id — and know that you are choosing to maintain the second record yourself.

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
  Settings/BrandingSettings.cs  text in Settings, logo attached in Media
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

Attachments are the media library's: `POST /api/media/{id}/attachments`,
`DELETE /api/media/{id}/attachments/{attachmentId}`, `PUT /api/media/attachments/order` and
`GET /api/media/roles/{entityName}`. The roles themselves are declared once, in `Program.cs`:

```csharp
.DeclareRoles("Product",  roles => roles.One("primary").Many("gallery"))
.DeclareRoles("Category", roles => roles.One("banner"))
.DeclareRoles<BrandingSettings>()   // reads [MediaRole] off the class
```

Add a role there — or one more `[MediaRole]` attribute — and the editor grows a box for it, with no
change to the dashboard. `DeclareRolesFrom(assembly)` scans a whole assembly if you would rather
every entity carried its own.

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
