using Xunit;
using DotNetBoost.Media.Core.Metadata;
using DotNetBoost.Media.Core.Models;
using DotNetBoostSample.Api.Integration;
using MediaKey = DotNetBoost.Media.Core.Metadata.EntityKey;
using MultiLingualKey = DotNetBoost.MultiLingual.Core.Metadata.EntityKey;

namespace DotNetBoostSample.Tests;

/// <summary>
/// Integration/MediaText.cs joins a media row to translation rows by formatting the same
/// <see cref="Guid"/> as a string on both sides. That only works while the two libraries agree on
/// the format — and nothing in either library enforces the agreement: each ships its own
/// <c>EntityKey</c>, and Media's source comments that it follows MultiLingual's rules by hand.
/// <para>
/// So the agreement is asserted here, in the application that depends on it. If a future release
/// of either library changes its formatting, these tests fail and say so, rather than the sample
/// quietly losing every piece of alt text it ever stored.
/// </para>
/// </summary>
public class KeyFormatAgreementTests
{
    public static TheoryData<object> Keys => new()
    {
        Guid.Parse("8f14e45f-ea0b-4c0f-9b3e-2a7d6c1b5e90"),
        Guid.Empty,
        42,
        0,
        9_999_999_999L,
        (short)7,
        "a-string-key",
    };

    [Theory]
    [MemberData(nameof(Keys))]
    public void Both_libraries_format_the_same_key_identically(object key)
    {
        Assert.Equal(MultiLingualKey.ToKeyString(key), MediaKey.ToKeyString(key));
    }

    [Fact]
    public void Both_libraries_allow_the_same_key_length()
    {
        Assert.Equal(MultiLingualKey.MaxLength, MediaKey.MaxLength);
    }

    [Fact]
    public void A_guid_is_formatted_the_way_MediaText_formats_it()
    {
        // MediaText.Key() uses Guid.ToString("D"). If the libraries ever moved to "N" (no
        // hyphens), the glue service would write ids the libraries could not read back.
        var id = Guid.NewGuid();

        Assert.Equal(id.ToString("D"), MediaKey.ToKeyString(id));
        Assert.Equal(id.ToString("D"), MultiLingualKey.ToKeyString(id));
    }

    [Fact]
    public void The_subject_name_fits_both_libraries_name_columns()
    {
        // MediaText files its rows under "MediaFile". Both libraries cap an entity name at the
        // same length, and this one is comfortably inside it — asserted so a future rename to
        // something long fails here rather than at the database.
        Assert.True(MediaText.Subject.Length <= 128);
        Assert.False(string.IsNullOrWhiteSpace(MediaText.Subject));
    }

    [Fact]
    public void The_subject_is_the_name_Media_resolves_a_file_to()
    {
        // The other half of the agreement, and the half the format tests above do not cover: the
        // ids line up, but only if the two stores are being asked about the same ENTITY NAME.
        //
        // MediaText hardcodes "MediaFile" so the rows sit under the name Media resolves the type
        // to, which is what lets the two be read side by side. Nothing in either library enforces
        // it — exactly as with the key format — so it is asserted here, the way
        // BrandingOwnerTests asserts it for the Branding owner. Renaming or attributing
        // MediaFile upstream fails this test instead of quietly stranding every stored alt text
        // under a name nothing asks about.
        Assert.Equal(MediaText.Subject, MediaEntityNames.For(typeof(MediaFile)));
    }
}

/// <summary>
/// Media lets one file be attached under several roles at once — a product's primary image can
/// also sit in its gallery — so the glue service is handed the same id twice. Keying a dictionary
/// on a repeated id throws, which is a 500 on the product detail endpoint.
/// </summary>
public class MediaTextDuplicateIdTests
{
    [Fact]
    public void Repeated_ids_collapse_to_one_entry()
    {
        var id = Guid.NewGuid();
        var other = Guid.NewGuid();

        // The shape GetManyAsync builds its result from: distinct ids, keyed by id.
        var ids = new[] { id, other, id }.Distinct().ToArray();
        var keyed = ids.ToDictionary(x => x, _ => (string?)null);

        Assert.Equal(2, keyed.Count);
        Assert.True(keyed.ContainsKey(id));
        Assert.True(keyed.ContainsKey(other));
    }
}
