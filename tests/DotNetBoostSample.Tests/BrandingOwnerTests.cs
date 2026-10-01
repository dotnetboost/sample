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

/// <summary>
/// What <c>[MultiLingual]</c> on <c>BrandingSettings.StoreName</c> does and does not achieve.
/// </summary>
public class BrandingTranslatableMetadataTests
{
    [Fact]
    public void The_attribute_is_valid_metadata_and_is_read()
    {
        // The attribute is not malformed and not ignored: MultiLingual's own metadata reports
        // StoreName as translatable. Nothing is wrong with it — there is simply no consumer.
        var translatable = DotNetBoost.MultiLingual.Core.Metadata.MultiLingualMetadata
            .For(typeof(BrandingSettings))
            .Select(p => p.Name)
            .ToList();

        Assert.Contains("StoreName", translatable);
        Assert.DoesNotContain("AccentColor", translatable);
    }

    [Fact]
    public void But_the_type_is_not_an_entity_so_nothing_ever_sees_it()
    {
        // The SaveChanges interceptor iterates ChangeTracker.Entries() and keys off
        // entry.Metadata.ClrType. BrandingSettings is never added to a DbContext — SettingManager
        // decomposes it into Setting rows — so it never appears there, and the interceptor's first
        // guard returns before it looks at anything.
        //
        // The entity that IS tracked when settings are written is Setting, which declares nothing.
        Assert.False(
            DotNetBoost.MultiLingual.Core.Metadata.MultiLingualMetadata
                .IsMultiLingual(typeof(DotNetBoost.Settings.Core.Models.Setting)),
            "Setting declares no translatable properties, so the interceptor skips the save entirely.");
    }
}

/// <summary>
/// What <c>[MultiLingualEntity]</c> changes, and what it does not.
/// </summary>
public class BrandingTranslationSubjectTests
{
    [Fact]
    public void The_translation_subject_is_the_name_the_attribute_fixes()
    {
        Assert.Equal(
            BrandingSettings.MediaOwnerName,
            DotNetBoost.MultiLingual.Core.Metadata.MultiLingualMetadata
                .EntityNameFor(typeof(BrandingSettings)));
    }

    [Fact]
    public void Both_libraries_now_address_branding_as_the_same_subject()
    {
        // Media's owner name and MultiLingual's entity name agree — so one (name, id) pair would
        // carry the logo attachment and StoreName's translations. They agree only because the same
        // word is written twice: the libraries share no registry, and this test is the only thing
        // that would report it if one of the two attributes changed.
        Assert.Equal(
            DotNetBoost.Media.Core.Metadata.MediaEntityNames.For(typeof(BrandingSettings)),
            DotNetBoost.MultiLingual.Core.Metadata.MultiLingualMetadata
                .EntityNameFor(typeof(BrandingSettings)));
    }
}
