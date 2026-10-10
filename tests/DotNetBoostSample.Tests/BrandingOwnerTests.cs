using System.Reflection;
using System.Text.RegularExpressions;
using DotNetBoost.Media.Core.Attributes;
using DotNetBoost.Media.Core.Metadata;
using DotNetBoostSample.Application.Settings;
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
            // The entity's own key is not a reference to anything — it is how MultiLingual
            // addresses this group. Only a name that points at something ELSE counts.
            p.Name != "Id"
            && (p.PropertyType == typeof(Guid)
                || p.PropertyType == typeof(Guid?)
                || (p.PropertyType == typeof(string)
                    && (p.Name.EndsWith("Id", StringComparison.Ordinal)
                        || p.Name.Contains("File", StringComparison.OrdinalIgnoreCase))));

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

    /// <summary>
    /// The dashboard addresses branding's media with two string literals of its own, and this is
    /// what stops them drifting from the constants they mirror.
    ///
    /// <para><b>Why they exist at all.</b> The logo is an attachment on the owner
    /// <c>("Branding", "branding")</c>, and nothing the API publishes tells a client that address.
    /// <c>GET /api/settings</c> answers <c>{ route, name, requiresAuthorization }</c>, where
    /// <c>name</c> is the CLR class name — <c>BrandingSettings</c>, which
    /// <c>GET /api/media/roles/BrandingSettings</c> knows nothing about, because
    /// <c>[MediaEntity]</c> pins the stored name to <c>Branding</c> on purpose. So the dashboard
    /// writes the two strings down, and <c>settings/branding.vue</c> says it is mirroring these
    /// constants.</para>
    ///
    /// <para><b>Why a comment is not enough.</b> This is the same failure mode the test above
    /// guards, one layer further out: the roles endpoint answers <c>200 []</c> for an owner name
    /// nobody declared rather than refusing it, so a dashboard asking about the wrong name sees a
    /// group with no media and renders nothing. No request fails, no log records it, and the logo
    /// has simply gone from the page. Renaming <c>MediaOwnerName</c> in C# should break the build,
    /// not the UI.</para>
    ///
    /// <para>Reading a <c>.vue</c> file from a C# test is unusual, and it is the cheap option
    /// here. The alternative is a group-level hook in DotNetBoost.Settings that publishes the
    /// owner address so the client can stop guessing — worth asking for, and not something this
    /// repository can assert its way to.</para>
    /// </summary>
    [Fact]
    public void The_dashboard_addresses_branding_by_the_name_and_id_the_API_declares()
    {
        var page = RepositoryRoot()
            .GetDirectories("clients").Single()
            .GetFiles("branding.vue", SearchOption.AllDirectories)
            .SingleOrDefault(f => f.Directory!.Name == "settings");

        // A failure, never a skip: a test that reports success without having run is worse than
        // no test. If the page moves, this says so rather than quietly passing forever.
        Assert.True(
            page is not null,
            "clients/dashboard/app/pages/settings/branding.vue was not found. If it moved, point "
            + "this test at it; if it was deleted because the dashboard no longer needs the "
            + "owner address written down, delete this test with it.");

        var source = File.ReadAllText(page!.FullName);

        Assert.Equal(BrandingSettings.MediaOwnerName, Literal(source, "ownerEntityName"));
        Assert.Equal(BrandingSettings.MediaOwnerId,   Literal(source, "ownerEntityId"));
    }

    /// <summary>
    /// The value of <c>const &lt;name&gt; = '…'</c> in a script, or a failure naming what it
    /// looked for.
    /// </summary>
    private static string Literal(string source, string name)
    {
        var match = Regex.Match(source, $@"const\s+{Regex.Escape(name)}\s*=\s*['""]([^'""]*)['""]");

        Assert.True(
            match.Success,
            $"No `const {name} = '…'` in settings/branding.vue. The dashboard still has to know "
            + "branding's media owner address, so if this was renamed or replaced, this test needs "
            + "to follow it.");

        return match.Groups[1].Value;
    }

    /// <summary>
    /// The repository root, found by walking up from the test assembly to the solution file.
    /// <para>
    /// Rather than a relative path from the output directory, which changes with configuration and
    /// target framework.
    /// </para>
    /// </summary>
    private static DirectoryInfo RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (directory.GetFiles("DotNetBoostSample.slnx").Length > 0) return directory;
        }

        throw new InvalidOperationException(
            $"No DotNetBoostSample.slnx above {AppContext.BaseDirectory}, so the repository root "
            + "could not be found.");
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

/// <summary>
/// The get-only <c>Id</c>: what it gives MultiLingual, and what it deliberately hides from Settings.
/// </summary>
public class BrandingIdTests
{
    [Fact]
    public void MultiLingual_can_now_key_the_group()
    {
        var accessor = new DotNetBoost.MultiLingual.Core.Services.ConventionEntityKeyAccessor();

        Assert.Equal(BrandingSettings.MediaOwnerId, accessor.GetKey(new BrandingSettings()));
    }

    [Fact]
    public void The_settings_library_cannot_see_it()
    {
        // SettingManager maps `.Where(p => p.CanRead && p.CanWrite)`. A get-only property fails
        // that, so it never becomes a row — which is the only reason adding an Id here is safe.
        var id = typeof(BrandingSettings).GetProperty(nameof(BrandingSettings.Id))!;

        Assert.True(id.CanRead);
        Assert.False(id.CanWrite, "A settable Id would be persisted as a setting row.");
    }

    [Fact]
    public void The_two_halves_of_the_address_are_now_both_present()
    {
        var name = DotNetBoost.MultiLingual.Core.Metadata.MultiLingualMetadata
            .EntityNameFor(typeof(BrandingSettings));

        var id = new DotNetBoost.MultiLingual.Core.Services.ConventionEntityKeyAccessor()
            .GetKey(new BrandingSettings());

        Assert.Equal("Branding", name);
        Assert.Equal("branding", id);
    }
}

/// <summary>
/// The <c>Id</c> must stay out of both the store and the API response. Being get-only handles the
/// first; only <c>[JsonIgnore]</c> handles the second, and the difference is easy to miss because
/// the table stays clean either way.
/// </summary>
public class BrandingIdIsNotGroupDataTests
{
    [Fact]
    public void It_is_not_serialised_with_the_group()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new BrandingSettings());

        Assert.DoesNotContain("\"id\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("storeName", json, StringComparison.OrdinalIgnoreCase);
    }
}
