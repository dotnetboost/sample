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

That one selector moves **both** languages in the dashboard. The content language is a row in
DotNetBoost.MultiLingual that the API resolves from `Accept-Language`; the dashboard's own labels,
buttons, help text and toasts are static bundles under `clients/dashboard/i18n/locales` —
English, Arabic and French, the three the sample seeds. Nothing makes them the same thing, so
`app/plugins/ui-language.ts` is the one file that decides they move together. A second switcher
for the chrome would be a second answer to the same question, and two answers can disagree.

Add a language on the Languages page and it has content long before it has a bundle: the labels
fall back to English, and the shell still flips if the language reads right-to-left, because
direction travels with the language row rather than with the translation file.

The choice is **remembered**, so the dashboard reopens in the language you left it in. Its
direction is remembered beside it — a cache of the API's last answer, not a list of RTL codes in
the client — so a remembered Arabic opens right-to-left on the first paint instead of flipping a
round trip later. A language that was deleted or deactivated in the meantime falls back to the
default rather than leaving the selector stuck on a code that no longer exists.

Counts are pluralised by CLDR rules rather than by an English `-s`, which is why two products
read **منتجان** in Arabic — the dual — and zero reads *0 produit* in French. Dates, relative times
and file sizes come from `Intl` in the UI locale, so a size is `1.4MB`, `1,4Mo` or `1.4 م.ب`
without a unit name being written down anywhere.

The sidebar header is the whole sample in miniature: the store's **name** is a
DotNetBoost.Settings value, its **logo** is a DotNetBoost.Media attachment, the **size** that logo
is drawn at is another setting, and nothing but `BrandingEndpoints.cs` knows all three exist. Change
any of them on the Settings page and the header follows on the next read — no restart, no redeploy.

**Settings** is DotNetBoost.Settings' own dashboard design: a tab per `[SettingGroup]`, and a form
*generated from whatever the group returns* rather than written out — a `bool` becomes a switch, an
`int` a number spinner, a `string` an input. Add a property to the C# class and the field appears;
remove one and it goes, which is exactly what happened when `LogoFileId` left `BrandingSettings`.

Saves are conditional: the `GET` returns an `ETag`, the `POST` sends it back as `If-Match`, and a
save that would overwrite someone else's is refused with `412`. The form then offers to re-apply
only the fields *you* edited on top of the latest values, so neither writer clobbers the other.
Verified by changing a group out-of-band mid-edit.

The second tab, **Catalog**, is there to show a group that is read somewhere other than the page
that writes it — three values, each landing in a different place:

| | |
|---|---|
| `PageSize` | How many products `GET /api/products` answers with. The catalogue list asks for no page size, so this is the one that decides; the pager under the list follows it. |
| `CurrencySymbol` | The glyph beside a price. Only the glyph — `Intl` places it, so the same `€` reads `€349.00` in English and `349.00 €` in Arabic. |
| `ShowProductsWithoutImages` | Turn it off and a product with nothing attached is not listed at all. The list says so rather than leaving a short page looking like the whole catalogue. |

Change `PageSize` from 12 to 2, reload the Catalog page, and it pages by two — no restart, no
redeploy, which is the entire claim of a settings library and is worth seeing actually happen.

**Languages** is DotNetBoost.MultiLingual's own page: a table with the code, the name written in
its own script and direction, the direction badge, the default marker, an active switch, and a
per-row menu for edit, make-default, *delete its translations* and delete. Refusals are shown in
the API's words — deactivating the default answers "The default language cannot be deactivated.
Promote another language to default first", and the switch snaps back.

Leaving Direction as "from the code" lets the API derive it from the runtime's culture data:
adding `he` comes back right-to-left without being told.

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

The products list is **paged**, and it does not decide the page size itself: it sends none, so the
API answers with `CatalogSettings.PageSize` and reports in `take` what it used. The pager reads that
number back rather than holding a second opinion about it, which is why editing the setting moves
the list and why the two cannot end up disagreeing. Prices are drawn with the same group's
`CurrencySymbol`.

Saving and deleting are reported in **the API's words** rather than the dashboard's: a delete reads
"تم حذف التصنيف coques-telephone." and not "Deleted", because the sentence naming the row comes back
on the write's own response, in the language the list is being read in. A category that still has
products is refused in the same language. [What a catalogue write answers
with](#what-a-catalogue-write-answers-with) has the shapes.

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
is re-read in that language, every label and button on the page is re-rendered in it, and Arabic
flips the shell to RTL.

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

> **Do not assign to what `GetAsync` hands you.** It is the instance `SettingManager` cached, not a
> copy, so `branding.StoreName = translated` writes one request's language into the entry every
> later reader of the group is served from. `BrandingEndpoints.cs` resolves the translated name into
> a local and projects it into the response instead. This is the kind of bug a cache hides rather
> than causes: with `Settings/LanguageAwareSettingCache.cs` giving each language its own entry, the
> stray write lands in the one language it was already right for and nothing looks wrong until
> somebody simplifies the cache away.

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
| **No cascade** | Deleting a `MediaFile` does not delete its alt text; the media library has no idea it exists. Both delete paths write that cleanup out by hand — `DELETE /api/products/{id}` for a product's files, and the Media page's own delete, which goes to the library's endpoint and so would otherwise strand the rows it never hears about. Two callers, because there are two ways to delete a file. |
| **Magic strings** | `"MediaFile"` has no compile-time link to the type. Mitigated with one `const`, and asserted against `MediaEntityNames.For(typeof(MediaFile))`. |
| **No shared guarantee** | Both libraries must format keys identically. They do — but by a hand-maintained convention, which is why `tests/KeyFormatAgreementTests.cs` asserts it rather than trusting it. |
| **One query the interface cannot ask** | `ShowProductsWithoutImages` needs "has an image" for every product at once, before a page can be taken. Media addresses one owner at a time and ships no bulk API, so `CatalogEndpoints.ProductsWithAnImage` reads the `MediaAttachments` table directly rather than running a query per row. It is the only place in the application that reads a library's rows instead of its interface, and it is a schema dependency a package upgrade could break where `IMediaAccessor` could not. |

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
  `string` key to the same string, and cap it at the same length. It also pins the other half of
  the agreement: ids only find each other if both stores are asked about the same *entity name*, so
  `MediaText.Subject` is asserted equal to `MediaEntityNames.For(typeof(MediaFile))`. Renaming
  `MediaFile` upstream fails here rather than stranding every stored alt text under a name nothing
  asks about. Together, this is what makes `MediaText.cs` safe.
- **`BrandingOwnerTests`** — the branding logo has exactly one record of it, the owner name the
  code uses is the one the attribute stores under, and the group's `Id` stays out of both the
  settings table and the JSON.

---

## Layout

```
src/DotNetBoostSample.Api/
  Data/AppDbContext.cs                   one database, three Apply* calls
  Data/Seed.cs                           schema + languages + catalogue on first run
  Entities/                              Category, Product — the app's own two entities
  Settings/BrandingSettings.cs           text in Settings, logo attached in Media
  Settings/LanguageAwareSettingCache.cs  a cache entry per group PER LANGUAGE
  Integration/MediaText.cs               Media × MultiLingual  ← the composition
  Endpoints/                             catalogue, media-text, branding
  Program.cs                             three independent registrations

aspire/DotNetBoostSample.AppHost/        postgres + api + dashboard
aspire/DotNetBoostSample.ServiceDefaults/ telemetry, health, discovery
clients/dashboard/                       Nuxt 4 + Nuxt UI, sidebar shell, RTL-aware
  i18n/locales/{en,ar,fr}.json           the dashboard's own labels, one file per locale
  i18n/i18n.config.ts                    CLDR plural rules for Arabic and French
  app/plugins/ui-language.ts             moves the UI locale with the reading language
  app/plugins/reading-language.ts        restores the remembered language before the first paint
  app/composables/useShellLocale.ts      direction and Nuxt UI's strings, from the API's answer
  app/utils/locale.ts                    the locale, for helpers that cannot call useI18n()
  app/utils/rememberedLanguage.ts        the choice, and its direction, across visits
  app/pages/settings.vue                 tabbed shell — one tab per settings group
  app/pages/settings/index.vue           Branding tab: the group form plus its media
  app/pages/settings/catalog.vue         Catalog tab
  app/components/settings/GroupForm.vue  form generated from whatever the group returns
  app/composables/useSettingsGroup.ts    conditional save: ETag in, If-Match out, 412 merge
  app/composables/useCatalogSettings.ts  the same group, read-only, for the screens it drives
  app/utils/settings.ts                  the group registry — one entry per tab
  app/pages/languages.vue                MultiLingual's own languages table
  app/components/languages/FormModal.vue add or edit a language
  app/composables/useLanguages.ts        create, update, set-default, delete, purge
  app/pages/catalog.vue                  Categories | Products tabs — list, edit, delete
  app/components/CategoryEditor.vue      slug + per-language text + banner picker
  app/components/ProductEditor.vue       columns + per-language text + primary & gallery
  app/components/TranslationEditor.vue   a button per language, fields for the selected one
  app/components/media/PickerModal.vue   pick an existing file from the library, or upload
  app/utils/format.ts                    sizes, dates and prices through Intl in the UI locale
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

`GET /api/products` takes `page` and `pageSize` and answers `{ items, total, skip, take }` — the
shape DotNetBoost.Media answers its own listings with, so the dashboard pages a catalogue the way
it already pages the media library. Both parameters are optional: with no `pageSize` the API uses
`CatalogSettings.PageSize`, which is how that setting reaches a screen at all, and `take` in the
response says what it settled on. `MaxPageSize` caps whatever is asked for, because a page size is
a caller's suggestion and an unbounded one is how a listing endpoint becomes a way to exhaust
memory.

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

### What a catalogue write answers with

`GET /api/products` comes back translated without anything asking it to, because MultiLingual's EF
Core interceptors did it. The **API's own sentences** are a separate question: a 404, a 409, and
"the category was saved" are not entity content, so no interceptor touches them.

They are localized anyway — `src/DotNetBoostSample.Api/Localization/ApiMessages.cs` is the marker
type they are injected under, and `Resources/{en,ar,fr}.json` beside it holds the words. What makes
this different from `AddLocalization()` is which language it reads:

```csharp
builder.Services
    .AddMultiLingual()
    .UseEntityFrameworkCore<AppDbContext>()
    .AddJsonLocalization()      // the API's own messages, same registration chain
    .Build();
```

The standard localizer answers in `CultureInfo.CurrentUICulture`. This one answers in
`IMultiLingualManager.CurrentLanguage` — the same language the interceptors just translated the row
with, resolved from the same `Accept-Language` and falling back the same way. So a refusal and the
content it was about cannot disagree about what language the caller reads.

Every write says what it did, not only what it refused:

| | Answers | Body |
|---|---|---|
| `POST /api/categories`, `POST /api/products` | `201` | `{ id, message }` |
| `PUT /api/categories/{id}`, `PUT /api/products/{id}` | `200` | `{ message }` |
| `DELETE /api/categories/{id}`, `DELETE /api/products/{id}` | `200` | `{ message }` |
| `POST /api/products/{id}/images` | `200` | `{ id, fileName, size, message }` |

The updates and deletes answer `200` rather than `204` for the obvious reason: a `204` is defined to
have no body, so there is nowhere to put the sentence. Refusals carry the same words in the `detail`
of RFC 7807 problem details, which is what MultiLingual's own endpoint filter already produces —
through the very same localizer — so the dashboard reads one shape whether the application refused
or the library did.

```console
$ curl -X DELETE /api/categories/3 -H 'Accept-Language: ar'
409  {"detail":"انقل منتجات هذا التصنيف أو احذفها أولًا."}

$ curl -X DELETE /api/products/4 -H 'Accept-Language: fr'
200  {"message":"Le produit CASE-002 a été supprimé."}

$ curl -X DELETE /api/categories/3 -H 'Accept-Language: ar'
200  {"message":"تم حذف التصنيف coques-telephone."}
```

The message names the row by its **slug or SKU**, not by the id the request addressed it with —
`"Category phone-cases was saved."` confirms the edit landed on the row the editor meant, where
`"Category 7 was saved."` sends them to go and check. A 404 keeps using the id —
`"La catégorie 3 est introuvable."` — because there the id is the only handle the caller has.

The dashboard splits the toast accordingly: the **title** is its own i18n bundle, because a short
label that is the same every time is what makes a toast scannable, and the **description** is the
API's sentence, because it names the row and the API is the side that knows which row it turned out
to be. Which is already how a refusal is shown — the same split, from `problemMessage(error)`.

> **Two gaps this sample surfaced.**
>
> `DotNetBoost.MultiLingual.API` ships `MapLanguageEndpoints` but no translation-editing
> endpoints, so writing translations over HTTP is the application's job —
> `Endpoints/MediaTextEndpoints.cs` and `Endpoints/CatalogEndpoints.cs` do it.
>
> `DotNetBoost.Media.API`'s list endpoint returns `url` as a *signed, expiring* link, which is
> null on the FileSystem backend. The dashboard therefore derives every URL from the file's id
> (`contentUrl` in `app/utils/format.ts`) rather than reading the field — which also means
> switching to Azure or S3 changes no client code.

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
