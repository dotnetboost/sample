using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Metadata;
using DotNetBoost.MultiLingual.Core.Models;
using DotNetBoostSample.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetBoostSample.Infrastructure.Persistence;

/// <summary>
/// Creates the schema and puts enough in it to see all three libraries working on first run.
/// A real application would use <c>dotnet ef migrations</c>; a sample you start with one command
/// should not make you run two.
/// </summary>
public static class DatabaseInitialiser
{
    public static async Task InitialiseDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

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

        db.Categories.AddRange(furniture, lighting);

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

        db.Products.AddRange(desk, chair, lamp);

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
}
