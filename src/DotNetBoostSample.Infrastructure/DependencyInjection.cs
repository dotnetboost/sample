using DotNetBoost.Auditing.Core;
using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.MultiLingual.Core;
using DotNetBoost.MultiLingual.Core.Models;
using DotNetBoost.Settings.Core.Interfaces;
using DotNetBoost.Settings.Core.Models;
using DotNetBoostSample.Application.Abstractions.Persistence;
using DotNetBoostSample.Application.Composition;
using DotNetBoostSample.Application.Settings;
using DotNetBoostSample.Domain.Catalog;
using DotNetBoostSample.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetBoostSample.Infrastructure;

/// <summary>
/// Where the four libraries' data actually goes — the blocks that used to be the body of
/// <c>Program.cs</c>, moved whole.
///
/// <para><b>What did not change in the move.</b> Still independent registrations, one block each,
/// in any order, with no <c>AddDotNetBoost()</c> and no package pairing any two of them. That is
/// the sample's subject, and a restructure that quietly introduced a coordinating call here would
/// have deleted the thing it was reorganising.</para>
///
/// <para><b>And what did not change when the fourth arrived.</b> Auditing is a fourth block of
/// the same shape — <c>AddAuditing()</c>, a store, <c>Build()</c> — appended without a line of the
/// three above moving. It is the cheapest possible evidence for the claim: adding a library here
/// is additive, because there is nothing for it to be wired into.</para>
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the database and all four stores.
    ///
    /// <para><b>Why this takes strings rather than <c>IConfiguration</c>.</b> Reading configuration
    /// is the host's job — it is the one that knows about <c>appsettings.json</c>, Aspire's
    /// injected connection strings and its own content root. Handing this method the two values it
    /// needs keeps the infrastructure layer off <c>Microsoft.Extensions.Configuration</c> and
    /// <c>Hosting</c> altogether, so its package list is EF Core, the three libraries' stores and
    /// nothing else. It also means the failure for a missing connection string is raised where it
    /// can say something useful about the AppHost, rather than here.</para>
    /// </summary>
    /// <param name="services">The container.</param>
    /// <param name="connectionString">PostgreSQL, from the AppHost.</param>
    /// <param name="mediaRoot">An absolute path for file bytes.</param>
    /// <param name="baseLanguage">
    /// <c>MultiLingualOptions.BaseLanguage</c>, or null for the mode this sample runs in by
    /// default.
    /// <para>
    /// Set it — <c>MultiLingual:BaseLanguage</c> in configuration, read by the host — and
    /// translatable properties KEEP their column, that column holds this language, and every
    /// other language stays a translation row. It is a property of the SCHEMA: it decides what
    /// <c>ApplyMultiLingual(this)</c> maps, so it cannot be changed on a database that already
    /// has content without moving that content between columns and rows.
    /// </para>
    /// <para>
    /// A string rather than a <c>bool</c> because the library's own option is a language code,
    /// and it arrives from configuration for the same reason the connection string does: reading
    /// configuration is the host's job. What it changes in this application is four writes and two
    /// reads, all of them through <c>Catalog/Shared/TranslatedText.cs</c> — the one file that asks
    /// whether it is set.
    /// </para>
    /// </param>
    /// <param name="exposeTranslations">
    /// Which subjects' translations DotNetBoost.MultiLingual routes over HTTP.
    /// <para>
    /// A delegate rather than a block below, because <c>ExposeOverHttp</c> is declared in
    /// DotNetBoost.MultiLingual.<b>API</b> — and that is the right place for it. Which translations
    /// are REACHABLE is a decision about this application's HTTP surface; where they are STORED is
    /// this file's business. Referencing the API package here to keep the chain in one place would
    /// have put routing in the infrastructure layer, so the host passes its answer in instead.
    /// </para>
    /// </param>
    /// <param name="configureAuditing">
    /// Whatever the host wants to add to the audit engine that needs ASP.NET Core.
    /// <para>
    /// A delegate for the same reason <paramref name="exposeTranslations"/> is one, and it is the
    /// same shape of decision: <c>UseHttpContextActor()</c> lives in
    /// DotNetBoost.Auditing.<b>API</b>, because WHO is acting is a fact about the current request
    /// and this layer has no requests. Where the trail is STORED is this file's business; whose
    /// name goes on an entry is the host's.
    /// </para>
    /// <para>
    /// Left null, the engine's default accessor answers null and every entry's actor is honestly
    /// unknown — which is what a background job or a migration would record, and is a better
    /// default than inventing "system".
    /// </para>
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string mediaRoot,
        string? baseLanguage = null,
        Action<MultiLingualBuilder>? exposeTranslations = null,
        Action<AuditingBuilder>? configureAuditing = null)
    {
        // ── The database ─────────────────────────────────────────────────────
        // One PostgreSQL database holds the application's two tables and every library table.
        // `UseMultiLingual(sp)` adds MultiLingual's EF Core interceptors, which is what makes a
        // plain `db.Products.ToListAsync()` in a handler come back already translated into the
        // request's language. It needs the (sp, options) overload of AddDbContext, so the
        // connection string is read from configuration by the host rather than through
        // AddNpgsqlDbContext.
        //
        // `UseAuditing(sp)` adds DotNetBoost.Auditing's SaveChangesInterceptor, which reads the
        // change tracker on every save and records what moved — so no handler mentions auditing.
        // Two independent libraries' interceptors on one context, chained in a line and knowing
        // nothing of each other. What it took to make that true is in the Auditing block below.
        //
        // `ReplaceService` is the one addition the base language asked for here: the model this
        // context builds depends on that option, and EF's own cache key does not. See
        // MultiLingualModelCacheKeyFactory — it costs nothing in either mode and makes the two
        // able to coexist in one process.
        services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseNpgsql(connectionString)
                   .UseMultiLingual(sp)
                   .UseAuditing(sp)
                   .ReplaceService<IModelCacheKeyFactory, MultiLingualModelCacheKeyFactory>());

        // The application layer's view of the same context — the two DbSets and SaveChangesAsync.
        // Resolved from the registration above, not registered a second time, so a handler and the
        // seeder are inside the same unit of work when they are in the same scope.
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // What a refused constraint means, in PostgreSQL's vocabulary. The only registration here
        // that exists because of the provider rather than because of a library — and the reason
        // DbUpdateExceptionHandler can answer 409 without the application layer naming Npgsql.
        services.AddScoped<IDatabaseErrorTranslator, PostgresErrorTranslator>();

        // ── DotNetBoost.Settings ─────────────────────────────────────────────
        // Runtime-editable configuration, stored in the Settings table of the same database.
        //
        // The library's own cache keys a group by its name, which is right while no value in a
        // group varies by language — and none does. `Setting` declares nothing translatable, so
        // MultiLingual's interceptor never sees a settings write, and branding's translated name is
        // resolved by BrandingProjector, in the application layer, by projecting a translation into
        // the RESPONSE, leaving the cached group untouched.
        //
        // This did carry a `.UseCustomCache<LanguageAwareSettingCache>()` that appended the
        // request's language to every key and evicted across all of them. It was correct, and it
        // guarded nothing: the one value that reads differently per language never reaches the
        // cache. A group whose values genuinely do differ per language needs it back — git history
        // has it, and the shape to copy is a language suffix on read plus eviction wider than the
        // write.
        services
            .AddSettings()
            .UseEntityFrameworkCore<AppDbContext>()
            // Scans the APPLICATION assembly for AbstractValidator<T>, so every write is checked —
            // and registers an ISettingSchemaContributor over those same rules, so
            // GET /api/settings/{group}/schema publishes them. Each group's rules sit beside the
            // group — see BrandingSettingsValidator: the bounds the dashboard's form draws its
            // controls from come from here instead of being hardcoded in the client.
            //
            // It is the application assembly rather than this one, and rather than the host's,
            // because that is where the groups live now. Naming the wrong assembly here would be a
            // silent loss of every rule: writes would stop being validated and the schema endpoint
            // would answer with no constraints at all — nothing fails, the dashboard just starts
            // drawing unbounded inputs. SettingsSchemaTests is what notices.
            .UseFluentValidation(typeof(BrandingSettings).Assembly)
            // And one of this application's own, publishing `translated` for a property carrying
            // [MultiLingual]. DotNetBoost.Settings cannot know that attribute exists — it belongs
            // to another library — so the schema endpoint takes it from here.
            //
            // Contributors MERGE rather than replace, so storeName comes back with
            // FluentValidation's `required` and `maxLength` as well as this one's flag. Order
            // decides a collision — first registered wins — and there is none here: the two
            // describe different constraints.
            .UseSchemaContributor<MultiLingualSchemaContributor>()
            // And the read-path shape for one group: GET /api/settings/branding answers with its
            // text in the request's language and its logo, neither of which is a setting. Three
            // libraries joined in one class, on the group's own endpoint, so there is one URL for
            // branding rather than two.
            .UseProjector<BrandingSettings, BrandingProjector>()
            // And the audit trail of a settings edit: one entry per group saved, with every value
            // that moved, rather than one per row of the Settings table. The interceptor would
            // record the rows — see the Auditing block, which ignores them for this — but a row is
            // one property of one group under an id nobody reading the trail knows, and this
            // library already knows where an edit begins and ends. SettingGroupAuditObserver has
            // what that costs.
            .UseWriteObserver<SettingGroupAuditObserver>()
            .Build();

        // ── DotNetBoost.Media ────────────────────────────────────────────────
        // Rows in PostgreSQL, bytes on local disk under the root the host resolved. Switching to
        // Azure or S3 is one line here and nothing else — including nothing in MediaText.cs, and
        // nothing in any slice.
        services
            .AddMedia()
            .WithMaxFileSize(25 * 1024 * 1024)
            .AllowContentTypes("image/*", "application/pdf")
            .DeclareRoles("Product",  roles => roles.One("primary").Many("gallery"))
            .DeclareRoles("Category", roles => roles.One("banner"))
            // Branding declares its roles on the class instead, with [MediaRole] — so they sit next
            // to the thing that has them rather than in this file. The owner it files them under
            // comes from that class's [MediaEntity], and it has no table behind it at all: an owner
            // is only ever two strings, so the library does not care whether a row exists. That is
            // what lets the dashboard draw branding's media with the same component it draws a
            // product's with.
            .DeclareRoles<BrandingSettings>()
            .UseEntityFrameworkCore<AppDbContext>()
            .UseFileSystem(mediaRoot, isDefault: true)
            // When a file goes, its per-language alt text goes with it. Registered once here rather
            // than remembered beside every delete — including the deletes this application does not
            // make, like DotNetBoost.Media's own DELETE /api/media/{id}, which used to strand those
            // rows. The handler is told the files that were actually deleted and not the ones merely
            // detached, so an image still in use somewhere else keeps its text.
            .OnFilesDeleted<MediaTextCleanup>()
            .Build();

        // ── DotNetBoost.MultiLingual ─────────────────────────────────────────
        // Translations and Languages tables in the same database. Languages are seeded by
        // DatabaseInitialiser.
        //
        // Two modes, one registration. Without a base language — the default here — a
        // translatable property has no column at all and every language, the default included, is
        // a translation row. With one, that property keeps its column, the column holds the base
        // language, and the rest stay rows. `ApplyMultiLingual(this)` in AppDbContext reads the
        // option off these services, so the mapping and the engine cannot disagree about which of
        // the two it is.
        var multiLingual = services
            // The one option this sample makes switchable. Assigned only when the host supplied
            // one, because the property validates what it is given and null is already the
            // default — `o.BaseLanguage = null` would be asking it to canonicalise nothing.
            //
            // Everything downstream reads the answer back off the library rather than off this
            // string: ILanguageAccessor.BaseLanguage and .IsBaseLanguage(code) are how the
            // application layer asks, so nothing has to be told twice.
            .AddMultiLingual(options =>
            {
                if (!string.IsNullOrWhiteSpace(baseLanguage)) options.BaseLanguage = baseLanguage;
            })
            .UseEntityFrameworkCore<AppDbContext>()
            // The API's OWN messages, from Resources/{en,ar,fr}.json in the host's content root,
            // through IStringLocalizer<ApiMessages>.
            //
            // Translated content is data in the Translations table; a message like "Category 3 was
            // not found." is code, so it lives in a file next to the code. What this adds over the
            // framework's AddLocalization() is which language it answers in:
            // IMultiLingualManager.CurrentLanguage, the one the EF interceptors just translated the
            // row with — not CultureInfo.CurrentUICulture. A refusal and the content it was about
            // therefore cannot disagree.
            .AddJsonLocalization();

        // What the host said is reachable over HTTP. See the parameter's documentation for why it
        // arrives as a delegate rather than being spelled out here.
        exposeTranslations?.Invoke(multiLingual);

        multiLingual.Build();

        // ── DotNetBoost.Auditing ─────────────────────────────────────────────
        // Who changed what, when, and from what to what — in the AuditEntries and
        // AuditPropertyChanges tables of the same database.
        //
        // Note what is NOT in this block, because it is the library's headline and it does hold:
        // nothing was added to Category or Product. Domain still references one package, still for
        // one attribute, and it is MultiLingual's. A trail is addressed by (entityName, entityId)
        // in two tables of its own, so no column, no collection and no migration appears on an
        // entity because it is audited.
        //
        // ── What it took to attach the capture to THIS context ───────────────
        //
        // `o.UseAuditing(sp)` above records every change this context saves, and two things
        // stood between this application and that line. Both are written down here because they
        // are what happens when two libraries that share nothing meet in one DbContext.
        //
        //   1. Translation. DotNetBoost.MultiLingual saves one of these beside every translated
        //      entity, keyed on four columns (EntityName, EntityId, PropertyName, LanguageCode).
        //      An audit entry records a single key, so the capture cannot address it — and nobody
        //      here owns the type to put [AuditIgnore] on it. `IgnoreEntity<Translation>()` below
        //      is that attribute, applied from the outside: the class-level [AuditIgnore] for a
        //      type the application cannot annotate. Neither library learned the other exists.
        //      Translated text is out of the trail anyway — see Product.Name — so ignoring its
        //      rows loses nothing the trail was meant to carry.
        //
        //   2. Keys. A creation's entry is addressed by the new row's id, and a key the database
        //      generates does not exist until the INSERT has run — so for such an entity the
        //      interceptor writes the Created entry in a SECOND SaveChanges, nested inside the
        //      first one's SavedChanges, under a transaction it opens itself. On this context that
        //      nested save runs through MultiLingual's interceptor as well, while MultiLingual is
        //      somewhere between capturing a [MultiLingual] entity's text and writing it — and a
        //      second save of a just-created entity is exactly how this application once lost a
        //      product's name, an empty one written over the stored one (git history has
        //      CreationTrail.cs, the workaround it needed). Whether the nested save is safe depends
        //      on the order the two interceptors run in and on internals neither library promises.
        //      So Category and Product take their keys from HiLo sequences instead (see their
        //      configurations): the id is assigned when the entity is ADDED, every creation's entry
        //      goes into the caller's own save, and there is no second save to reason about. The
        //      libraries' own entities already assign their keys in code.
        //
        //   3. Settings. DotNetBoost.Settings stores a group as one row per property, so the
        //      capture recorded a settings save as a `Setting` entry per value that moved, each
        //      addressed by a row id. `IgnoreEntity<Setting>()` below keeps those rows out, and
        //      SettingGroupAuditObserver — registered on the Settings block above — records the
        //      save instead, as one `SettingGroup` entry addressed by the group's name.
        //
        // What that buys is the guarantee the library is built around: a change and its entry are
        // in one SaveChanges, so they commit together or neither does. The one exception is the
        // settings entry, written after the settings save has committed — see the observer. And
        // every other mapped entity is captured unless it is ignored — MediaFile, MediaAttachment
        // and Language as well as the catalogue — because that is the library's default.
        var auditing = services
            .AddAuditing()
            // The trail goes in AppDbContext, not in a store of its own, and that is what the
            // transaction guarantee rests on: AppendAsync TRACKS the entry rather than saving it,
            // so a handler that records before its SaveChangesAsync writes the change and the
            // entry in one transaction. The library's Dapper and MongoDb stores write when they
            // are called instead, and atomicity is then the application's to arrange.
            .UseEntityFrameworkCore<AppDbContext>()
            // The application's own two entities, by type. The interceptor reads a type's
            // [AuditIgnore] and [AuditRedact] rules off the tracked entity itself, so this is not
            // what applies them; it is what GET /api/audit/entities lists and describes, and what
            // a prune would read a per-entity retention from.
            //
            // DeclareEntities rather than DeclareEntitiesFrom(assembly), because the scan declares
            // the types that CARRY one of the library's attributes and these two carry none —
            // which is the point above restated.
            .DeclareEntities(typeof(Category), typeof(Product))
            // DotNetBoost.MultiLingual's row, kept out of the trail — see above. Build() refuses a
            // type that is both declared and ignored, and the declaration above names only this
            // application's own two, so the two lines cannot disagree.
            .IgnoreEntity<Translation>()
            // DotNetBoost.Settings' row, kept out for the same reason from the other direction:
            // the observer above records a settings save as a whole group, and leaving the rows in
            // would record it twice.
            .IgnoreEntity<Setting>();

        // No retention is configured, deliberately. Retention does nothing until a prune runs,
        // this sample schedules none, and POST /api/audit/prune is not mapped — so a number here
        // would state a policy that nothing in the solution carries out. The trail grows forever,
        // which for a sample seeded on first run is the honest answer; `o.Retention` plus a hosted
        // service that calls PruneAsync is the real one.

        // And whose name goes on an entry, which only the host can answer. See the parameter.
        configureAuditing?.Invoke(auditing);

        auditing.Build();

        return services;
    }
}
