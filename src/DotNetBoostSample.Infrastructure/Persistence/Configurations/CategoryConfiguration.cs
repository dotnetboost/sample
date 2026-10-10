using DotNetBoostSample.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetBoostSample.Infrastructure.Persistence.Configurations;

/// <summary>
/// The Categories table.
///
/// <para>Note what is <b>not</b> configured: <c>Name</c> and <c>Description</c>. They carry
/// <c>[MultiLingual]</c>, and <c>ApplyMultiLingual(this)</c> in <see cref="AppDbContext"/> strips
/// them off the model entirely — they are rows in the translations table, not columns here. Adding
/// a <c>Property(c =&gt; c.Name)</c> below would be configuring a column that is never
/// created.</para>
/// </summary>
public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    /// <summary>
    /// The unique index on <c>Slug</c>, named here rather than left to EF's convention.
    ///
    /// <para>Because <see cref="PostgresErrorTranslator"/> reads it: a refused insert carries the
    /// constraint's name and nothing else useful, and that is how the refusal becomes "another
    /// category already uses that slug" instead of a generic conflict. Pinning the name as a
    /// constant means the index and the sentence about it cannot drift apart — a renamed index
    /// would otherwise silently fall through to the generic message.</para>
    /// </summary>
    public const string SlugIndex = "IX_Categories_Slug";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Category> category)
    {
        category.ToTable("Categories");
        category.HasKey(c => c.Id);
        category.Property(c => c.Slug).HasMaxLength(128).IsRequired();
        category.HasIndex(c => c.Slug).IsUnique().HasDatabaseName(SlugIndex);
    }
}
