using DotNetBoost.Media.EntityFrameworkCore;
using DotNetBoost.MultiLingual.EntityFrameworkCore;
using DotNetBoost.Settings.EntityFrameworkCore;
using DotNetBoostSample.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DotNetBoostSample.Api.Data;

/// <summary>
/// One PostgreSQL database, one DbContext, three libraries' tables alongside the application's
/// own two.
/// <para>
/// This class is the only place in the solution where all three libraries are named together —
/// and note what that now amounts to: three independent <c>Apply*</c> calls in
/// <see cref="OnModelCreating"/> and nothing else. No interface to implement and no <c>DbSet</c>
/// to declare: each store reaches its own tables through <c>Set&lt;T&gt;()</c>, and this
/// application goes through <c>ISettingManager</c>, <c>IMediaManager</c> and
/// <c>IMultiLingualManager</c> rather than the tables. Removing a library means deleting its one
/// line here. Nothing else in the file changes.
/// </para>
/// <para>
/// Until preview.2 each library also asked for a marker interface and a pair of <c>DbSet</c>
/// properties — six properties and three interfaces that the stores never read. They are gone,
/// which is a better demonstration of the independence this sample is about than a class
/// implementing three interfaces to prove it.
/// </para>
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // ── The application's own entities ───────────────────────────────────────
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product>  Products   => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // The application's own mapping.
        modelBuilder.Entity<Category>(category =>
        {
            category.ToTable("Categories");
            category.HasKey(c => c.Id);
            category.Property(c => c.Slug).HasMaxLength(128).IsRequired();
            category.HasIndex(c => c.Slug).IsUnique();
        });

        modelBuilder.Entity<Product>(product =>
        {
            product.ToTable("Products");
            product.HasKey(p => p.Id);
            product.Property(p => p.Sku).HasMaxLength(64).IsRequired();
            product.Property(p => p.Price).HasPrecision(18, 2);
            product.HasIndex(p => p.Sku).IsUnique();
            product.HasOne(p => p.Category)
                   .WithMany(c => c.Products)
                   .HasForeignKey(p => p.CategoryId)
                   .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Three independent calls, in any order ────────────────────────────
        // Settings: the Settings and SettingAudits tables, with PostgreSQL column types.
        // The engine read off the context rather than named here, so switching provider in
        // Program.cs needs no matching edit. The explicit
        // ApplySettingsConfiguration(DatabaseProvider.PostgreSql) overload is still there for
        // pinning it — a model built against one engine and migrated onto another.
        modelBuilder.ApplySettingsConfiguration(this);

        // Media: the MediaFiles and MediaAttachments tables.
        modelBuilder.ApplyMedia();

        // MultiLingual: the Translations and Languages tables — and, passing `this`, it also
        // strips every [MultiLingual] string property off Category and Product, so those columns
        // are never created. That is why Products has no Name column.
        modelBuilder.ApplyMultiLingual(this);
    }
}
