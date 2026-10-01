using DotNetBoost.Media.Core.Models;
using DotNetBoost.Media.EntityFrameworkCore;
using DotNetBoost.MultiLingual.Core.Models;
using DotNetBoost.MultiLingual.EntityFrameworkCore;
using DotNetBoost.Settings.Core.Models;
using DotNetBoost.Settings.EntityFrameworkCore;
using DotNetBoostSample.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DotNetBoostSample.Api.Data;

/// <summary>
/// One PostgreSQL database, one DbContext, three libraries' tables alongside the application's
/// own two.
/// <para>
/// This class is the only place in the solution where all three libraries are named together —
/// and note what that amounts to: implementing three independent interfaces and calling three
/// independent <c>Apply*</c> methods. None of them requires the others, none of them knows the
/// others are here, and removing any one of them means deleting its interface, its DbSets and its
/// one line in <see cref="OnModelCreating"/>. Nothing else in the file changes.
/// </para>
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), ISettingDbContext, IMediaDbContext, IMultiLingualDbContext
{
    // ── The application's own entities ───────────────────────────────────────
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product>  Products   => Set<Product>();

    // ── DotNetBoost.Settings (ISettingDbContext) ─────────────────────────────
    public DbSet<Setting>           Settings      => Set<Setting>();
    public DbSet<SettingAuditEntry> SettingAudits => Set<SettingAuditEntry>();

    // ── DotNetBoost.Media (IMediaDbContext) ──────────────────────────────────
    public DbSet<MediaFile>       MediaFiles       => Set<MediaFile>();
    public DbSet<MediaAttachment> MediaAttachments => Set<MediaAttachment>();

    // ── DotNetBoost.MultiLingual (IMultiLingualDbContext) ────────────────────
    public DbSet<Translation> Translations => Set<Translation>();
    public DbSet<Language>    Languages    => Set<Language>();

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
        modelBuilder.ApplySettingsConfiguration(DatabaseProvider.PostgreSql);

        // Media: the MediaFiles and MediaAttachments tables.
        modelBuilder.ApplyMedia();

        // MultiLingual: the Translations and Languages tables — and, passing `this`, it also
        // strips every [MultiLingual] string property off Category and Product, so those columns
        // are never created. That is why Products has no Name column.
        modelBuilder.ApplyMultiLingual(this);
    }
}
