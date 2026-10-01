using DotNetBoost.MultiLingual.Core.Interfaces;
using DotNetBoost.MultiLingual.Core.Models;
using DotNetBoostSample.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DotNetBoostSample.Api.Data;

/// <summary>
/// Creates the schema and puts enough in it to see all three libraries working on first run.
/// A real application would use <c>dotnet ef migrations</c>; a sample you start with one command
/// should not make you run two.
/// </summary>
public static class Seed
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

        await multiLingual.UseLanguageAsync("en");

        // ── Categories and products, in three languages ──────────────────────
        var furniture = new Category { Slug = "furniture", Name = "Furniture", Description = "Tables, chairs and desks." };
        var lighting  = new Category { Slug = "lighting",  Name = "Lighting",  Description = "Lamps and fixtures." };

        db.Categories.AddRange(furniture, lighting);

        multiLingual.For(furniture)
            .Set("ar", c => c.Name, "أثاث")
            .Set("ar", c => c.Description, "طاولات وكراسي ومكاتب.")
            .Set("fr", c => c.Name, "Mobilier")
            .Set("fr", c => c.Description, "Tables, chaises et bureaux.");

        multiLingual.For(lighting)
            .Set("ar", c => c.Name, "إضاءة")
            .Set("ar", c => c.Description, "مصابيح وتركيبات.")
            .Set("fr", c => c.Name, "Éclairage")
            .Set("fr", c => c.Description, "Lampes et luminaires.");

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

        multiLingual.For(desk)
            .Set("ar", p => p.Name, "مكتب كتابة من البلوط")
            .Set("ar", p => p.Description, "بلوط صلب، بدرجين.")
            .Set("fr", p => p.Name, "Bureau en chêne")
            .Set("fr", p => p.Description, "Chêne massif, deux tiroirs.");

        multiLingual.For(chair)
            .Set("ar", p => p.Name, "كرسي من الجوز")
            .Set("ar", p => p.Description, "جوز منحني، مقعد منسوج.")
            .Set("fr", p => p.Name, "Chaise en noyer")
            .Set("fr", p => p.Description, "Noyer cintré, assise tressée.");

        multiLingual.For(lamp)
            .Set("ar", p => p.Name, "مصباح مكتب نحاسي")
            .Set("ar", p => p.Description, "ذراع قابل للتعديل، ضوء دافئ.")
            .Set("fr", p => p.Name, "Lampe de bureau en laiton")
            .Set("fr", p => p.Description, "Bras réglable, lumière chaude.");

        await db.SaveChangesAsync();
    }
}
