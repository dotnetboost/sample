using DotNetBoost.MultiLingual.Core.Interfaces;
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

        // The language this seed's own values are authored in — and the one every translatable
        // property assigned below is therefore filed under. It has to be resolved before the
        // first save: a translatable property is content like any other to the interceptor, and
        // this is what says which language that content is in.
        //
        // In base-language mode it is also, deliberately, the base language the README documents:
        // the text below is English, and an entity may only be CREATED in the language its own
        // columns hold. Those assignments then go to the columns rather than to "en" rows, which
        // is the whole of what that mode changes here.
        await multiLingual.UseLanguageAsync("en");

        // ── Categories and products, in three languages ──────────────────────
        //
        // The other two languages go through this, rather than a Set() chain per entity, for one
        // reason: in base-language mode a Set() naming the base language is refused — those values
        // are the entity's own columns, written by the object initialisers below — so the base
        // language has to be skipped rather than written twice. Inert in the default mode, where nothing is the base
        // language and all three are rows; what keeps a first run from failing for someone who
        // configures a base language this English seed was not written for.
        void Translate<T>(T entity, string language, string name, string description)
            where T : class
        {
            if (multiLingual.Languages.IsBaseLanguage(language)) return;

            multiLingual.For(entity).Set(language, new Dictionary<string, string?>
            {
                ["Name"] = name,
                ["Description"] = description
            });
        }

        var furniture = new Category { Slug = "furniture", Name = "Furniture", Description = "Tables, chairs and desks." };
        var lighting  = new Category { Slug = "lighting",  Name = "Lighting",  Description = "Lamps and fixtures." };

        db.Categories.AddRange(furniture, lighting);

        Translate(furniture, "ar", "أثاث", "طاولات وكراسي ومكاتب.");
        Translate(furniture, "fr", "Mobilier", "Tables, chaises et bureaux.");

        Translate(lighting, "ar", "إضاءة", "مصابيح وتركيبات.");
        Translate(lighting, "fr", "Éclairage", "Lampes et luminaires.");

        await db.SaveChangesAsync();

        var desk = new Product
        {
            Sku = "DESK-001", Price = 349.00m, CategoryId = furniture.Id,
            Name = "Oak writing desk", Description = "Solid oak, two drawers."
        };

        var chair = new Product
        {
            Sku = "CHAIR-001", Price = 189.00m, CategoryId = furniture.Id,
            Name = "Walnut chair", Description = "Bent walnut, woven seat."
        };

        var lamp = new Product
        {
            Sku = "LAMP-001", Price = 79.00m, CategoryId = lighting.Id,
            Name = "Brass desk lamp", Description = "Adjustable arm, warm light."
        };

        db.Products.AddRange(desk, chair, lamp);

        Translate(desk, "ar", "مكتب كتابة من البلوط", "بلوط صلب، بدرجين.");
        Translate(desk, "fr", "Bureau en chêne", "Chêne massif, deux tiroirs.");

        Translate(chair, "ar", "كرسي من الجوز", "جوز منحني، مقعد منسوج.");
        Translate(chair, "fr", "Chaise en noyer", "Noyer cintré, assise tressée.");

        Translate(lamp, "ar", "مصباح مكتب نحاسي", "ذراع قابل للتعديل، ضوء دافئ.");
        Translate(lamp, "fr", "Lampe de bureau en laiton", "Bras réglable, lumière chaude.");

        await db.SaveChangesAsync();
    }
}
