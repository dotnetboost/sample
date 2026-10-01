using System.Reflection;
using DotNetBoost.Media.Core.Attributes;
using DotNetBoost.Media.Core.Metadata;
using DotNetBoostSample.Api.Settings;
using Xunit;

namespace DotNetBoostSample.Tests;

/// <summary>
/// The branding logo has exactly one record of it: a media attachment. These tests are the guard
/// on that, because the failure mode of adding a second one is silent — the two drift, and the
/// only symptom is a logo that survives being detached.
/// </summary>
public class BrandingOwnerTests
{
    [Fact]
    public void BrandingSettings_holds_no_reference_to_a_file()
    {
        // What is banned is a second record of WHICH FILE is the logo — nothing can keep two in
        // step, because DotNetBoost.Media ships no change hook, so a detach on the media page
        // could never write back here.
        //
        // A property merely named after the logo is fine: HeaderLogoSize is an int describing how
        // the logo is drawn, not which file it is. So this looks at the type, and only treats a
        // name as evidence when it is a type that could hold an identifier.
        static bool CouldIdentifyAFile(PropertyInfo p) =>
            p.PropertyType == typeof(Guid)
            || p.PropertyType == typeof(Guid?)
            || (p.PropertyType == typeof(string)
                && (p.Name.EndsWith("Id", StringComparison.Ordinal)
                    || p.Name.Contains("File", StringComparison.OrdinalIgnoreCase)));

        var suspicious = typeof(BrandingSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(CouldIdentifyAFile)
            .Select(p => $"{p.PropertyType.Name} {p.Name}")
            .ToList();

        Assert.True(
            suspicious.Count == 0,
            $"BrandingSettings has {string.Join(", ", suspicious)}. The logo is a media attachment "
            + "on (\"Branding\", \"branding\"); a file reference here would be a second record of "
            + "the same fact, and the two cannot be kept in step.");
    }

    [Fact]
    public void The_owner_name_the_code_uses_is_the_one_the_attribute_stores_under()
    {
        // DeclareRoles<BrandingSettings>() files the roles under MediaEntityNames.For(type), while
        // the endpoints address the owner with the constant. If those two ever disagree, roles are
        // declared for one owner and files attached to another — and nothing fails loudly.
        Assert.Equal(
            BrandingSettings.MediaOwnerName,
            MediaEntityNames.For(typeof(BrandingSettings)));
    }

    [Fact]
    public void The_owner_name_is_pinned_so_a_class_rename_cannot_orphan_attachments()
    {
        // Without [MediaEntity], the stored owner name follows the class name — rename the class
        // and every existing attachment addresses an owner nothing asks about any more.
        var pinned = typeof(BrandingSettings).GetCustomAttribute<MediaEntityAttribute>();

        Assert.NotNull(pinned);
        Assert.Equal(BrandingSettings.MediaOwnerName, pinned!.Name);
    }

    [Fact]
    public void The_logo_role_is_declared_on_the_class()
    {
        var roles = typeof(BrandingSettings).GetCustomAttributes<MediaRoleAttribute>().ToList();

        Assert.Contains(roles, r => r.Name == BrandingSettings.LogoRole && !r.Multiple);
    }

    [Fact]
    public void The_owner_id_is_usable_as_an_entity_key()
    {
        // The media library refuses a blank owner id and caps its length; a singleton's id is
        // degenerate but still has to be a valid key.
        Assert.False(string.IsNullOrWhiteSpace(BrandingSettings.MediaOwnerId));
        Assert.True(BrandingSettings.MediaOwnerId.Length <= EntityKey.MaxLength);
    }
}
