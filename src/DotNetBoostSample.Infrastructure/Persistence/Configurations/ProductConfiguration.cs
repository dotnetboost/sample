using DotNetBoostSample.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetBoostSample.Infrastructure.Persistence.Configurations;

/// <summary>
/// The Products table.
///
/// <para>There is no media column and no <c>Images</c> collection to configure: attachments live
/// in the media library's own table, keyed by the entity name <c>"Product"</c> and the row's id.
/// And as with a category, the translated <c>Name</c> and <c>Description</c> are not columns —
/// which is why <c>Products</c> has no <c>Name</c> column in the database at all.</para>
///
/// <para><c>OnDelete(Restrict)</c> is the database half of the refusal in <c>DeleteCategory</c>:
/// the handler answers 409 with a localized sentence, and this makes sure a path that did not
/// think to check cannot orphan a product either.</para>
/// </summary>
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Product> product)
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
    }
}
