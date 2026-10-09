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
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Category> category)
    {
        category.ToTable("Categories");
        category.HasKey(c => c.Id);
        category.Property(c => c.Slug).HasMaxLength(128).IsRequired();
        category.HasIndex(c => c.Slug).IsUnique();
    }
}
