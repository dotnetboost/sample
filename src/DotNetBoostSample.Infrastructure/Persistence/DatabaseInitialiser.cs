using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Metadata;
using DotNetBoost.MultiLingual.Core.Models;
using DotNetBoostSample.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetBoostSample.Infrastructure.Persistence;

/// <summary>
/// Creates the schema — including the fourth library's two tables — and puts enough in it to see
/// Settings, Media and MultiLingual working on first run. A real application would use
/// <c>dotnet ef migrations</c>; a sample you start with one command should not make you run two.
///
/// <para><b>The audit trail starts with the seed.</b> Everything below saves through the same
/// <c>DbContext</c> the requests do, and DotNetBoost.Auditing's interceptor is attached to it — so
/// the three languages, two categories and three products are each recorded as created, by nobody:
/// there is no request here and so no actor, and an unknown actor is the honest answer for a
/// startup job. Their translation rows are not, because <c>Translation</c> is ignored (see
/// <c>DependencyInjection.cs</c>). It happens once, on the run that creates the catalogue.</para>
/// </summary>
public static class DatabaseInitialiser
{
    public static async Task InitialiseDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<AppDbContext>();
        if (!await db.Database.EnsureCreatedAsync())
        {
            await db.CreateMissingTablesAsync();
            await db.RestoreIdentityKeysAsync();
        }

        var multiLingual = sp.GetRequiredService<IMultiLingualManager>();

        // ── Languages, before anything that writes a translation ─────────────
        var existing = await multiLingual.Languages.GetAllAsync();

        if (existing.Count == 0)
        {
            await multiLingual.Languages.CreateAsync(new NewLanguage
            {
                Code = "en", Name = "English", Direction = TextDirection.Ltr, IsDefault = true
            });

            // Arabic, right-to-left — the RTL-first case the library is built for.
            await multiLingual.Languages.CreateAsync(new NewLanguage
            {
                Code = "ar", Name = "العربية", Direction = TextDirection.Rtl
            });

            await multiLingual.Languages.CreateAsync(new NewLanguage
            {
                Code = "fr", Name = "Français", Direction = TextDirection.Ltr
            });
        }

        if (await db.Categories.AnyAsync()) return;

        // The language this seed AUTHORS in — the one whose values go onto the entities themselves
        // rather than into translation rows.
        //
        // The base language when there is one, because an entity may only be CREATED in the
        // language its own columns hold; "en" otherwise, which is this sample's default language
        // and what the seed read in before the mode existed. Resolved before the first save either
        // way: a translatable property is content like any other to the interceptor, and this is
        // what says which language that content is in.
        var authoring = multiLingual.Languages.BaseLanguage ?? "en";

        // Checked rather than assumed: UseLanguageAsync falls back to the DEFAULT language for one
        // it does not know. Harmless without a base language — there is nothing below that depends
        // on which one it is — but in base-language mode it would leave this seed authoring in a
        // language that is not the base one, and the library would then refuse the first create
        // with a message about the request's language rather than about the setting that caused it.
        if (!await multiLingual.UseLanguageAsync(authoring) && multiLingual.Languages.BaseLanguage is not null)
        {
            throw new InvalidOperationException(
                $"MultiLingual:BaseLanguage is '{authoring}', which is not an active language in "
                + "this database. The seed below carries en, ar and fr: set the option to one of "
                + "them, or create that language before the catalogue is seeded.");
        }

        // ── Categories and products, in three languages ──────────────────────
        //
        // One call per entity carrying every language, because the split between them is the only
        // thing the two modes disagree about here and it is not the seed's business to know which
        // mode is on:
        //
        //   • The AUTHORING language is ASSIGNED to the entity's own properties — filed as that
        //     language's rows in the default mode, and written to the columns in base-language
        //     mode. It has to be on the entity before the first save, not queued: a translatable
        //     property is re-read on every save in this scope, so an entity added with empty
        //     properties has that emptiness written back over its text by the next one.
        //
        //   • Every OTHER language is queued as a translation row, which is all it can be in
        //     either mode. Naming the authoring language here instead would be refused outright
        //     when it is the base one — those values are a column, not a translation.
        //
        // It is also what makes the seed right for ANY of the three languages it carries, rather
        // than only for "en": set the base language to "ar" and the Arabic text is what lands in
        // the columns, with English and French as the rows beside it. Nothing below is written
        // twice or in the wrong language, because every language is named here explicitly instead
        // of one of them being implied by the properties the entities were constructed with.
        void Text<T>(T entity, params (string Language, string Name, string Description)[] text)
            where T : class
        {
            foreach (var (language, name, description) in text)
            {
                var values = new Dictionary<string, string?>
                {
                    ["Name"] = name,
                    ["Description"] = description
                };

                if (!string.Equals(language, authoring, StringComparison.OrdinalIgnoreCase))
                {
                    multiLingual.For(entity).Set(language, values);
                    continue;
                }

                // By name rather than by `entity.Name = …`, so one helper serves both entity types.
                // A translatable property the seed forgot is a KeyNotFoundException here, which is
                // the right noise for a seed that is this application's own.
                foreach (var property in MultiLingualMetadata.For(typeof(T)))
                    property.SetValue(entity, values[property.Name] ?? string.Empty);
            }
        }

        var furniture = new Category { Slug = "furniture" };
        var lighting  = new Category { Slug = "lighting" };

        await db.Categories.AddRangeAsync(furniture, lighting);

        Text(furniture,
            ("en", "Furniture", "Tables, chairs and desks."),
            ("ar", "أثاث", "طاولات وكراسي ومكاتب."),
            ("fr", "Mobilier", "Tables, chaises et bureaux."));

        Text(lighting,
            ("en", "Lighting", "Lamps and fixtures."),
            ("ar", "إضاءة", "مصابيح وتركيبات."),
            ("fr", "Éclairage", "Lampes et luminaires."));

        await db.SaveChangesAsync();

        // The categories now have ids, which is what the three products are keyed to.
        var desk  = new Product { Sku = "DESK-001",  Price = 349.00m, CategoryId = furniture.Id };
        var chair = new Product { Sku = "CHAIR-001", Price = 189.00m, CategoryId = furniture.Id };
        var lamp  = new Product { Sku = "LAMP-001",  Price =  79.00m, CategoryId = lighting.Id  };

        await db.Products.AddRangeAsync(desk, chair, lamp);

        Text(desk,
            ("en", "Oak writing desk", "Solid oak, two drawers."),
            ("ar", "مكتب كتابة من البلوط", "بلوط صلب، بدرجين."),
            ("fr", "Bureau en chêne", "Chêne massif, deux tiroirs."));

        Text(chair,
            ("en", "Walnut chair", "Bent walnut, woven seat."),
            ("ar", "كرسي من الجوز", "جوز منحني، مقعد منسوج."),
            ("fr", "Chaise en noyer", "Noyer cintré, assise tressée."));

        Text(lamp,
            ("en", "Brass desk lamp", "Adjustable arm, warm light."),
            ("ar", "مصباح مكتب نحاسي", "ذراع قابل للتعديل، ضوء دافئ."),
            ("fr", "Lampe de bureau en laiton", "Bras réglable, lumière chaude."));

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Adds the tables the model has and an existing database does not — and nothing else.
    ///
    /// <para><c>EnsureCreated</c> does nothing to a database that already exists, and Aspire keeps
    /// this one on a data volume across runs. So a database created before a library joined the
    /// model — DotNetBoost.Auditing's <c>AuditEntries</c> and <c>AuditPropertyChanges</c>, the
    /// first time this branch runs against an older volume — never gets that library's tables, and
    /// every read of them is a 500. This creates them from EF's own create script, keeping its
    /// order, so the volume survives the upgrade with its data intact.</para>
    ///
    /// <para>Only missing tables and their indexes. A column added to an existing table is a
    /// migration, which this sample does not have; for that, drop the volume.</para>
    /// </summary>
    private static async Task CreateMissingTablesAsync(this AppDbContext db)
    {
        var existing = (await db.Database
                .SqlQueryRaw<string>(
                    "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = current_schema()")
                .ToListAsync())
            .ToHashSet(StringComparer.Ordinal);

        var missing = db.Model.GetEntityTypes()
            .Select(type => type.GetTableName())
            .OfType<string>()
            .Where(table => !existing.Contains(table))
            .ToHashSet(StringComparer.Ordinal);

        if (missing.Count == 0) return;

        var statements = db.Database.GenerateCreateScript()
            .Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(statement => CreatedTable(statement) is { } table && missing.Contains(table));

        foreach (var statement in statements)
            await db.Database.ExecuteSqlRawAsync(statement);
    }

    /// <summary>
    /// Gives each identity key back its identity, and moves its counter past the ids the table
    /// already holds.
    ///
    /// <para>The same upgrade as <see cref="CreateMissingTablesAsync"/>, for the keys. Category and
    /// Product took their ids from HiLo sequences for a while, and a volume from those builds is in
    /// one of two states. Created by them, the column is a plain integer: <c>EnsureCreated</c> gave
    /// it no default, so an INSERT that leaves the id to the database is refused. Created before
    /// them, the column kept its identity, but the HiLo builds sent their own ids, which the
    /// identity counter never saw, so the next one it hands out may be a row the table has.</para>
    ///
    /// <para>So: add the identity where it is missing, then move the counter past the highest id
    /// when it is behind it. Only forward, and every boot, so a database in step is left alone.</para>
    ///
    /// <para>The highest id is not only the highest row. A row the HiLo builds created and then
    /// deleted is gone from its table, but its audit trail outlives it, addressed by its id; hand
    /// that id out again and the new entity's trail begins with the old one's history. That
    /// happened on this sample's own data volume. So the counter also goes past the last block
    /// each HiLo sequence handed out: EF's HiLo generator takes the sequence's value and the
    /// increment's worth of ids after it. The sequences are left where they are; nothing reads them
    /// any more, and a process still running an older build would.</para>
    ///
    /// <para>Not safe while an older build is still running against the same database: it keeps
    /// handing out ids from the HiLo block it holds, which the identity counter knows nothing about.
    /// Stop the old build first.</para>
    /// </summary>
    private static async Task RestoreIdentityKeysAsync(this AppDbContext db)
    {
        // The two tables that had HiLo keys, and no other: every library's tables are that
        // library's to manage. With the sequences those builds drew from, which are history now and
        // so named here rather than read off the model.
        var keys = new[] { (typeof(Category), "CategoryIds"), (typeof(Product), "ProductIds") }
            .Select(key => (Type: db.Model.FindEntityType(key.Item1)!, HiLo: key.Item2))
            .Select(key => (
                Table: key.Type.GetTableName()!,
                Column: key.Type.FindPrimaryKey()!.Properties.Single().GetColumnName(),
                key.HiLo));

        foreach (var (table, column, hiLo) in keys)
        {
            // Identifiers, which no parameter can carry — and every one of them read off this
            // application's own model, not from input.
            var hasIdentity = await db.Database
                .SqlQueryRaw<string>(
                    "SELECT is_identity AS \"Value\" FROM information_schema.columns " +
                    "WHERE table_schema = current_schema() AND table_name = {0} AND column_name = {1}",
                    table, column)
                .SingleAsync();

            if (hasIdentity == "NO")
            {
                var identity = $"ALTER TABLE \"{table}\" ALTER COLUMN \"{column}\" ADD GENERATED BY DEFAULT AS IDENTITY";
                await db.Database.ExecuteSqlRawAsync(identity);
            }

            // The highest row, or the last id of the last HiLo block, whichever is higher; GREATEST
            // skips a NULL, which is what an empty table or a sequence never drawn from gives.
            // setval's default is_called = true, so the next id is one past it. A counter already
            // past both is left as it is.
            var align = $"""
                SELECT setval(s.seq, m.highest)
                FROM (SELECT pg_get_serial_sequence('"{table}"', '{column}')::regclass AS seq) s,
                     (SELECT GREATEST(
                         (SELECT MAX("{column}") FROM "{table}"),
                         (SELECT last_value + increment_by - 1 FROM pg_sequences
                          WHERE schemaname = current_schema() AND sequencename = '{hiLo}')) AS highest) m
                WHERE m.highest IS NOT NULL
                  AND m.highest > COALESCE(pg_sequence_last_value(s.seq), 0)
                """;

            await db.Database.ExecuteSqlRawAsync(align);
        }
    }

    /// <summary>The table a <c>CREATE TABLE</c> or <c>CREATE INDEX … ON</c> statement is about.</summary>
    private static string? CreatedTable(string statement)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            statement, "^CREATE (?:UNIQUE )?(?:TABLE|INDEX \\S+ ON) (?:\"[^\"]+\"\\.)?\"([^\"]+)\"");

        return match.Success ? match.Groups[1].Value : null;
    }
}
