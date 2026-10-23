using DotNetBoost.Auditing.EntityFrameworkCore;
using DotNetBoost.Media.EntityFrameworkCore;
using DotNetBoost.MultiLingual.EntityFrameworkCore;
using DotNetBoost.Settings.EntityFrameworkCore;
using DotNetBoostSample.Application.Abstractions.Persistence;
using DotNetBoostSample.Domain.Catalog;
using DotNetBoostSample.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace DotNetBoostSample.Infrastructure.Persistence;

/// <summary>
/// One PostgreSQL database, one DbContext, four libraries' tables alongside the application's
/// own two.
/// <para>
/// This class is the only place in the solution where all four libraries are named together —
/// and note what that now amounts to: four independent <c>Apply*</c> calls in
/// <see cref="OnModelCreating"/> and nothing else. No library interface to implement and no
/// <c>DbSet</c> to declare for them: each store reaches its own tables through
/// <c>Set&lt;T&gt;()</c>, and this application goes through <c>ISettingManager</c>,
/// <c>IMediaManager</c>, <c>IMultiLingualManager</c> and <c>IAuditManager</c> rather than the
/// tables. Removing a library means deleting its one line here. Nothing else in the file changes.
/// </para>
/// <para>
/// Auditing is the newest of the four and the one that says most about the arrangement. Its two
/// tables are mapped here like any other library's — but its <em>capture</em> is not attached to
/// this context, because <c>Translation</c> has a composite key that an audit entry cannot
/// address, and neither the attribute that would exclude it nor the key that would fix it belongs
/// to this application. So the catalogue's writes record themselves, one line per write slice.
/// <c>DependencyInjection.cs</c> has the whole finding; it is the one place where two of these
/// libraries, in one database, do not simply coexist.
/// </para>
/// <para>
/// It is also the <b>one</b> of the eight composition files that is infrastructure. The other
/// seven — <c>MediaText</c>, <c>MediaTextCleanup</c>, <c>MultiLingualSchemaContributor</c>,
/// <c>BrandingProjector</c>, <c>CreationTrail</c> and the two entities' attributes — depend on
/// <c>DotNetBoost.*.Core</c> and nothing else, so they live in the application and domain layers
/// beside the code that uses them. Joining two libraries turns out not to be a persistence
/// concern; joining them <em>in one database</em> is, and that is this file.
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
        // and so this method stays what it is worth reading for: the four library calls below.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ── Four independent calls, in any order ────────────────────────────
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

        // Auditing: the AuditEntries and AuditPropertyChanges tables. Two tables for every
        // entity in the application, now and later — an entry points AT a row by (entityName,
        // entityId) with deliberately no foreign key back, so auditing something new costs no
        // migration and a deleted product leaves its history standing. The last part is not a
        // side effect: the entry that records the deletion is the only copy of what the row said.
        //
        // Nothing is added to Category or Product by this call. That is the whole of the schema
        // side, and it is deliberately separate from what makes a change get recorded — which
        // here is the write slices calling IAuditManager, because the interceptor that would do
        // it for them cannot be attached to this context. See DependencyInjection.cs.
        modelBuilder.ApplyAuditing();
    }
}
