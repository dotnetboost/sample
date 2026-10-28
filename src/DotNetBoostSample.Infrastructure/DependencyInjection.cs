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
/// Where the four libraries' data goes: one independent block each, in any order, with no
/// <c>AddDotNetBoost()</c> and no package pairing any two of them. Adding a library here is
/// additive, because there is nothing for it to be wired into.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the database and all four stores.
    ///
    /// <para>It takes values rather than <c>IConfiguration</c> because reading configuration is the
    /// host's job, which keeps this layer's packages to EF Core and the four libraries' stores.</para>
    /// </summary>
    /// <param name="services">The container.</param>
    /// <param name="connectionString">PostgreSQL, from the AppHost.</param>
    /// <param name="mediaRoot">An absolute path for file bytes.</param>
    /// <param name="baseLanguage">
    /// <c>MultiLingualOptions.BaseLanguage</c>, or null for the mode this sample runs in by
    /// default. Set, translatable properties keep their column for this language and every other
    /// language stays a translation row. It decides what <c>ApplyMultiLingual(this)</c> maps, so it
    /// is a schema decision: it cannot change on a database that already has content.
    /// </param>
    /// <param name="exposeTranslations">
    /// Which subjects' translations DotNetBoost.MultiLingual routes over HTTP. A delegate because
    /// <c>ExposeOverHttp</c> lives in DotNetBoost.MultiLingual.API: what is reachable is the host's
    /// decision, where it is stored is this layer's.
    /// </param>
    /// <param name="configureAuditing">
    /// Audit-engine additions that need ASP.NET Core, such as <c>UseHttpContextActor()</c> — a
    /// delegate for the same reason. Left null, every entry's actor is honestly unknown, which is
    /// a better default than inventing "system".
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
        // One PostgreSQL database for the application's two tables and every library's.
        //
        // UseMultiLingual(sp) adds the interceptors that make a plain db.Products.ToListAsync()
        // come back translated; it needs the (sp, options) overload, hence AddDbContext rather than
        // AddNpgsqlDbContext. UseAuditing(sp) adds the interceptor that records every save, so no
        // handler mentions auditing. The two know nothing of each other; what it took for them to
        // share this context is in the Auditing block below.
        //
        // ReplaceService, because the model depends on the base language and EF's own cache key
        // does not. See MultiLingualModelCacheKeyFactory.
        services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseNpgsql(connectionString)
                   .UseMultiLingual(sp)
                   .UseAuditing(sp)
                   .ReplaceService<IModelCacheKeyFactory, MultiLingualModelCacheKeyFactory>());

        // The application layer's view of the same context, resolved from it rather than
        // registered again, so a handler and the seeder share one unit of work in one scope.
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // What a refused constraint means in PostgreSQL's vocabulary, so DbUpdateExceptionHandler
        // can answer 409 without the application layer naming Npgsql.
        services.AddScoped<IDatabaseErrorTranslator, PostgresErrorTranslator>();

        // ── DotNetBoost.Settings ─────────────────────────────────────────────
        // The library's cache keys a group by name, which is right because no stored value varies by
        // language: branding's translated name is projected into the response by
        // BrandingProjector, never written into the cached group.
        services
            .AddSettings()
            .UseEntityFrameworkCore<AppDbContext>()
            // Validates every write with the AbstractValidator<T>s beside each group, and publishes
            // their bounds in GET /api/settings/{group}/schema for the dashboard's form. It must be
            // the assembly the groups live in: name the wrong one and nothing fails — writes just
            // stop being validated. SettingsSchemaTests notices.
            .UseFluentValidation(typeof(BrandingSettings).Assembly)
            // Publishes `translated` for a property carrying [MultiLingual], which DotNetBoost.Settings
            // cannot know about. Contributors merge, so storeName keeps FluentValidation's
            // constraints as well.
            .UseSchemaContributor<MultiLingualSchemaContributor>()
            // GET /api/settings/branding with its text in the request's language and its logo.
            .UseProjector<BrandingSettings, BrandingProjector>()
            // One audit entry per group saved, rather than one per Settings row.
            .UseWriteObserver<SettingGroupAuditObserver>()
            .Build();

        // ── DotNetBoost.Media ────────────────────────────────────────────────
        // Rows in PostgreSQL, bytes on local disk. Azure or S3 would be one line here.
        services
            .AddMedia()
            .WithMaxFileSize(25 * 1024 * 1024)
            .AllowContentTypes("image/*", "application/pdf")
            .DeclareRoles("Product",  roles => roles.One("primary").Many("gallery"))
            .DeclareRoles("Category", roles => roles.One("banner"))
            // From the class's own [MediaRole], filed under its [MediaEntity] owner, which has no
            // table behind it: an owner is only ever two strings.
            .DeclareRoles<BrandingSettings>()
            .UseEntityFrameworkCore<AppDbContext>()
            .UseFileSystem(mediaRoot, isDefault: true)
            // A deleted file's alt text goes with it, whoever deleted it — including Media's own
            // DELETE /api/media/{id}. Only files actually deleted, not ones merely detached.
            .OnFilesDeleted<MediaTextCleanup>()
            .Build();

        // ── DotNetBoost.MultiLingual ─────────────────────────────────────────
        // Translations and Languages tables in the same database; languages are seeded by
        // DatabaseInitialiser. ApplyMultiLingual(this) reads the base language off these services,
        // so the mapping and the engine cannot disagree about the mode.
        var multiLingual = services
            // Assigned only when given, because the setter validates and null is the default.
            // Everything downstream asks the library (ILanguageAccessor.BaseLanguage) rather than
            // reading this string again.
            .AddMultiLingual(options =>
            {
                if (!string.IsNullOrWhiteSpace(baseLanguage)) options.BaseLanguage = baseLanguage;
            })
            .UseEntityFrameworkCore<AppDbContext>()
            // The API's own messages, from Resources/{en,ar,fr}.json, through
            // IStringLocalizer<ApiMessages>. Unlike AddLocalization() it answers in
            // IMultiLingualManager.CurrentLanguage — the language the content was translated into —
            // so a refusal and the content it is about cannot disagree.
            .AddJsonLocalization();

        exposeTranslations?.Invoke(multiLingual);

        multiLingual.Build();

        // ── DotNetBoost.Auditing ─────────────────────────────────────────────
        // Who changed what, when, and from what to what, in two tables of its own addressed by
        // (entityName, entityId) — so nothing is added to Category or Product.
        //
        // Three things had to be settled before UseAuditing(sp) above could go on this context:
        //
        //   1. Translation. MultiLingual's row is keyed on four columns and an audit entry on one,
        //      and nobody here owns the type to put [AuditIgnore] on it. IgnoreEntity<Translation>()
        //      is that attribute applied from outside. Translated text is out of the trail anyway.
        //
        //   2. Keys. For a database-generated key, the interceptor writes a Created entry in a
        //      second, nested SaveChanges — which on this context also runs through MultiLingual's
        //      interceptor mid-save, and once wrote an empty product name over the stored one.
        //      Category and Product take HiLo keys instead (see their configurations), so the id
        //      exists when the entity is added and every entry goes into the caller's own save.
        //
        //   3. Settings. The library stores a group one row per property. IgnoreEntity<Setting>()
        //      keeps those rows out, and SettingGroupAuditObserver records the save as one entry for
        //      the group instead — after the settings save commits, the one entry not written
        //      atomically with its change.
        //
        // Every other mapped entity is captured, because that is the library's default — MediaFile,
        // MediaAttachment and Language as well as the catalogue.
        var auditing = services
            .AddAuditing()
            // In AppDbContext, which is what makes a change and its entry commit together: the
            // EF store tracks an entry rather than saving it.
            .UseEntityFrameworkCore<AppDbContext>()
            // What GET /api/audit/entities lists. Named rather than scanned for, because the scan
            // finds types carrying the library's attributes and these two carry none.
            .DeclareEntities(typeof(Category), typeof(Product))
            .IgnoreEntity<Translation>()
            .IgnoreEntity<Setting>();

        // No o.Retention: retention is the runtime-editable `auditing` settings group, applied by
        // the host's AuditRetentionService. An option here is read once, at Build(), and could not
        // follow an edit.

        configureAuditing?.Invoke(auditing);

        auditing.Build();

        return services;
    }
}
