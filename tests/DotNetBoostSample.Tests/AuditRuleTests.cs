using DotNetBoost.Auditing.Core.Metadata;
using DotNetBoost.MultiLingual.Core.Metadata;
using DotNetBoostSample.Domain.Catalog;
using Xunit;

namespace DotNetBoostSample.Tests;

/// <summary>
/// The one rule this application adds to DotNetBoost.Auditing's model: <b>a property whose value
/// depends on the language is not in the trail.</b>
///
/// <para><b>Why it needs asserting.</b> An audit entry has no language column, so
/// <c>Name: "Desk" → "Writing desk"</c> would be a sentence about one of four languages and silent
/// about which — and in the mode the sample ships in, <c>Name</c> is not a column at all, so the
/// value a diff compares is whatever was hydrated for the reading language. The rule is stated with
/// <c>[AuditIgnore]</c> on <c>Category</c> and <c>Product</c>, where the engine reads it for every
/// way of recording, rather than filtered in the handlers that happen to record today.</para>
///
/// <para><b>And why it is asserted like this.</b> Not as a list of property names: the set of
/// translated properties is <c>[MultiLingual]</c>'s to decide and the set of ignored ones is
/// <c>[AuditIgnore]</c>'s, and the claim is that those two sets are the same. Written as a list,
/// the test would keep passing when someone adds a fifth translated property and forgets the second
/// attribute — which is exactly the mistake worth catching, because its only symptom is a trail
/// that confidently says the wrong thing.</para>
///
/// <para>No database and no host: two attributes, read through each library's own metadata. Kept on
/// <c>[Fact]</c> rather than a <c>[Theory]</c> over the two entity types, because every count in
/// the documentation is one test per method — see <see cref="DocumentedCountTests"/>.</para>
/// </summary>
public class AuditRuleTests
{
    /// <summary>The entities this application declares to the audit engine.</summary>
    private static readonly Type[] Audited = [typeof(Category), typeof(Product)];

    [Fact]
    public void A_translated_property_is_ignored_by_the_trail()
    {
        foreach (var entity in Audited)
        {
            var translated = MultiLingualMetadata.For(entity).Select(p => p.Name).Order().ToArray();

            var ignored = AuditSchema.Describe(entity)
                .Where(p => p.Rule == AuditPropertyRule.Ignore)
                .Select(p => p.Name)
                .Order()
                .ToArray();

            Assert.NotEmpty(translated);

            // Stated as the equality rather than two containment checks, so it fails in both
            // directions: a translated property that is recorded, and an ignored property that is
            // ignored for some other reason nobody wrote down.
            Assert.Equal(translated, ignored);
        }
    }

    /// <summary>
    /// And the other half, so the rule reads as a boundary rather than as "ignore things": the
    /// columns a trail <em>can</em> state a change about are recorded, with their values.
    ///
    /// <para><c>Slug</c> and <c>Sku</c> are the two properties this sample marks
    /// <c>[NotMultiLingual]</c> — the same string in every language — so they are precisely the
    /// ones the rule above must not touch. A reader who assumed "translated entity, no trail" would
    /// have both halves wrong.</para>
    /// </summary>
    [Fact]
    public void A_language_independent_column_is_recorded()
    {
        (Type Entity, string Property)[] recorded =
        [
            (typeof(Category), nameof(Category.Slug)),
            (typeof(Product), nameof(Product.Sku)),
            (typeof(Product), nameof(Product.Price))
        ];

        foreach (var (entity, property) in recorded)
        {
            Assert.Equal(
                AuditPropertyRule.Record,
                AuditSchema.Describe(entity).Single(p => p.Name == property).Rule);
        }
    }

    /// <summary>
    /// Nothing is redacted, which is worth pinning rather than leaving to be noticed: neither
    /// entity holds a secret, so an <c>[AuditRedact]</c> appearing here would mean some property's
    /// values are being withheld from the trail by a change nobody wrote a test for.
    /// </summary>
    [Fact]
    public void Nothing_in_the_catalogue_is_redacted()
    {
        foreach (var entity in Audited)
        {
            Assert.DoesNotContain(
                AuditPropertyRule.Redact,
                AuditSchema.Describe(entity).Select(p => p.Rule));
        }
    }
}
