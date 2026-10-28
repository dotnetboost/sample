using DotNetBoostSample.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetBoostSample.Infrastructure.Persistence.Configurations;

/// <summary>
/// The Products table.
///
/// <para>There is no media column and no <c>Images</c> collection to configure: attachments live
/// in the media library's own table, keyed by the entity name <c>"Product"</c> and the row's id.
/// And as with a category, where the translated <c>Name</c> and <c>Description</c> live is not
/// decided here — see <see cref="CategoryConfiguration"/>, which has the long version.</para>
///
/// <para><c>OnDelete(Restrict)</c> is the database half of the refusal in <c>DeleteCategory</c>:
/// the handler answers 409 with a localized sentence, and this makes sure a path that did not
/// think to check cannot orphan a product either.</para>
/// </summary>
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    /// <inheritdoc cref="CategoryConfiguration.SlugIndex"/>
    public const string SkuIndex = "IX_Products_Sku";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Product> product)
    {
        product.ToTable("Products");
        product.HasKey(p => p.Id);

        // HiLo, from a sequence of its own — see CategoryConfiguration for why a key assigned
        // before the save is what keeps a creation's audit entry in that save.
        product.Property(p => p.Id).UseHiLo("ProductIds");
        product.Property(p => p.Sku).HasMaxLength(64).IsRequired();
        product.Property(p => p.Price).HasPrecision(18, 2);
        product.HasIndex(p => p.Sku).IsUnique().HasDatabaseName(SkuIndex);
        product.HasOne(p => p.Category)
               .WithMany(c => c.Products)
               .HasForeignKey(p => p.CategoryId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
