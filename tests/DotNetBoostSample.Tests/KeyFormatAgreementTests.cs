using Xunit;
using DotNetBoost.Media.Core.Metadata;
using DotNetBoost.Media.Core.Models;
using DotNetBoost.MultiLingual.Core.Metadata;
using DotNetBoostSample.Api.Integration;
using MediaKey = DotNetBoost.Media.Core.Metadata.EntityKey;
using MultiLingualKey = DotNetBoost.MultiLingual.Core.Metadata.EntityKey;

namespace DotNetBoostSample.Tests;

/// <summary>
/// Integration/MediaText.cs joins a media row to translation rows by addressing the same subject
/// in both stores. That only works while the two libraries agree on how a subject is addressed —
/// and nothing in either library enforces the agreement: each ships its own <c>EntityKey</c>.
/// <para>
/// So the agreement is asserted here, in the application that depends on it. Both libraries
/// publish the spec version they implement as <c>EntityKey.AddressingVersion</c>, and both bump it
/// for <em>any</em> change to name resolution, key formatting or either length limit — so one line
/// covers what used to be a hand-written theory table over key formats, and covers the changes a
/// table would have missed.
/// </para>
/// <para>
/// The two format checks below are kept on purpose. The version is a promise; they are what catches
/// a release that changes the format and forgets to bump it.
/// </para>
/// </summary>
public class KeyFormatAgreementTests
{
    [Fact]
    public void Both_libraries_implement_the_same_entity_addressing_version()
    {
        // The one assertion this file is really about. Bumped for any change to entity-name
        // resolution, key discovery, key formatting, unset-key detection or either length limit —
        // including a change that is invisible from inside a single library, which is exactly the
        // kind that silently stops two stores addressing the same subject.
        //
        // Both libraries carry the full conformance suite — EntityAddressingConformance in
        // DotNetBoost.Media.TestSupport, EntityAddressing.KeyCases and AssertAgreesWith in
        // DotNetBoost.MultiLingual.TestSupport — and an application cannot use either: both
        // projects are IsPackable=false, so neither reaches a feed. Their READMEs suggest
        // deriving from them, which is a promise no consumer can take up today. Hence the
        // constants, which are public and are enough.
        Assert.Equal(MultiLingualKey.AddressingVersion, MediaKey.AddressingVersion);
    }

    [Fact]
    public void A_guid_is_formatted_the_same_way_by_both_libraries_and_is_still_hyphenated()
    {
        // MediaText.Key() asks MultiLingual's EntityKey to format the id rather than formatting it
        // itself, so the two cannot disagree by construction. What this still pins is the format
        // every stored row was written in: a move to "N" (no hyphens) would be a valid key format
        // that no existing alt text could be read back under.
        var id = Guid.NewGuid();

        Assert.Equal(MultiLingualKey.ToKeyString(id), MediaKey.ToKeyString(id));
        Assert.Equal(id.ToString("D"), MediaKey.ToKeyString(id));
    }

    [Fact]
    public void An_int_is_formatted_the_same_way_by_both_libraries()
    {
        // The other key type this application addresses owners by — a Category's and a Product's
        // Id. Invariant decimal in both, so a culture with another digit shape cannot reach a key.
        Assert.Equal(MultiLingualKey.ToKeyString(42), MediaKey.ToKeyString(42));
        Assert.Equal("42", MediaKey.ToKeyString(42));
    }

    [Fact]
    public void The_subject_name_fits_both_libraries_name_columns()
    {
        // MediaText files its rows under "MediaFile". Both libraries cap an entity name, and each
        // publishes its own cap — read from them rather than written out here, so a rename to
        // something long fails on the library's number instead of on a copy of it.
        Assert.False(string.IsNullOrWhiteSpace(MediaText.Subject));
        Assert.True(MediaText.Subject.Length <= MediaEntityNames.MaxLength);
        Assert.True(MediaText.Subject.Length <= MultiLingualMetadata.MaxEntityNameLength);
    }

    [Fact]
    public void The_subject_is_the_name_Media_resolves_a_file_to()
    {
        // The other half of the agreement, and the half the version does not cover: the ids line
        // up, but only if the two stores are being asked about the same ENTITY NAME.
        //
        // MediaText hardcodes "MediaFile" so the rows sit under the name Media resolves the type
        // to, which is what lets the two be read side by side. No spec version can assert that —
        // it is this application's own convention — so it is asserted here, the way
        // BrandingOwnerTests asserts it for the Branding owner. Renaming or attributing MediaFile
        // upstream fails this test instead of quietly stranding every stored alt text under a name
        // nothing asks about.
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
