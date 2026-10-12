using DotNetBoost.Media.EntityFrameworkCore;
using DotNetBoost.MultiLingual.EntityFrameworkCore;
using DotNetBoost.Settings.EntityFrameworkCore;
using DotNetBoostSample.Application.Abstractions.Persistence;
using DotNetBoostSample.Domain.Catalog;
using DotNetBoostSample.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace DotNetBoostSample.Infrastructure.Persistence;

/// <summary>
/// One PostgreSQL database, one DbContext, three libraries' tables alongside the application's
/// own two.
/// <para>
/// This class is the only place in the solution where all three libraries are named together —
/// and note what that now amounts to: three independent <c>Apply*</c> calls in
/// <see cref="OnModelCreating"/> and nothing else. No library interface to implement and no
/// <c>DbSet</c> to declare for them: each store reaches its own tables through
/// <c>Set&lt;T&gt;()</c>, and this application goes through <c>ISettingManager</c>,
/// <c>IMediaManager</c> and <c>IMultiLingualManager</c> rather than the tables. Removing a
/// library means deleting its one line here. Nothing else in the file changes.
/// </para>
/// <para>
/// It is also the <b>one</b> of the five composition files that is infrastructure. The other four
/// — <c>MediaText</c>, <c>MediaTextCleanup</c>, <c>MultiLingualSchemaContributor</c> and
/// <c>BrandingProjector</c> — depend on <c>DotNetBoost.*.Core</c> and nothing else, so they live
/// in the application layer beside the slices that use them. Joining two libraries turns out not
/// to be a persistence concern; joining them <em>in one database</em> is, and that is this file.
/// </para>
/// <para>
/// <see cref="IAppDbContext"/> is the application layer's view of it: the two <c>DbSet</c>s and
/// <c>SaveChangesAsync</c>, so a handler can write <c>db.Products.ToListAsync()</c> and have
/// MultiLingual's interceptors translate it, without Application naming the Npgsql provider.
/// </para>
/// <para>
/// Until preview.2 each library also asked for a marker interface and a pair of <c>DbSet</c>
/// properties — six properties and three interfaces that the stores never read. They are gone,
/// which is a better demonstration of the independence this sample is about than a class
/// implementing three interfaces to prove it.
/// </para>
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    // ── The application's own entities ───────────────────────────────────────
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product>  Products   => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── The application's own mapping ────────────────────────────────────
        // One IEntityTypeConfiguration per entity, in Configurations/, rather than two lambdas
        // here. Scanned rather than listed, so adding an entity adds a file and nothing else —
        // and so this method stays what it is worth reading for: the three library calls below.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ── Three independent calls, in any order ────────────────────────────
        // Settings: the Settings table, with PostgreSQL column types.
        // The engine read off the context rather than named here, so switching provider in
        // Program.cs needs no matching edit. The explicit
        // ApplySettings(DatabaseProvider.PostgreSql) overload is still there for
        // pinning it — a model built against one engine and migrated onto another.
        modelBuilder.ApplySettings(this);

        // Media: the MediaFiles and MediaAttachments tables.
        modelBuilder.ApplyMedia();

        // MultiLingual: the Translations and Languages tables — and, passing `this`, it also
        // decides where every [MultiLingual] string property lives, by reading the engine's
        // options off this context's application services.
        //
        // With no base language configured, which is how the sample runs by default, it strips
        // those properties off Category and Product and the columns are never created: Products
        // has no Name column at all. Configure one and they are kept, and that column holds the
        // base language. Passing `this` rather than nothing is what makes the mapping follow the
        // engine instead of the two having to be set to the same thing twice.
        modelBuilder.ApplyMultiLingual(this);
    }
}
