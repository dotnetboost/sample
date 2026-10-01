using DotNetBoost.Media.Core.Interfaces;
using DotNetBoost.Settings.Core.Interfaces;
using DotNetBoostSample.Api.Settings;

namespace DotNetBoostSample.Api.Integration;

/// <summary>
/// Keeps the branding logo visible to DotNetBoost.Media, by attaching it whenever the setting
/// changes.
///
/// <para><b>The problem this fixes.</b> A file referenced only by a setting's value is invisible to
/// the media library: it appears in no entity's attachments, the file's details panel says it is
/// used by nothing, and deleting it warns about nothing — right up until the site loses its logo.
/// Storing an id somewhere the library cannot see is the cost of that composition, and this is the
/// cure for it.</para>
///
/// <para><b>What owns what.</b> <see cref="BrandingSettings.LogoFileId"/> stays the single source of
/// truth; the attachment is a derived index maintained on write. The owner is
/// <c>("Branding", "branding")</c> — an owner with no table behind it at all, which the media
/// library is perfectly happy with because an owner is only ever two strings. That is also what
/// lets the dashboard use the same role box and the same picker here as it does for a product.</para>
///
/// <para><b>Idempotent on purpose.</b> It re-asserts the link from <c>current</c> rather than
/// acting only when the id changed, so changing anything in the group — the store name alone will
/// do — also repairs a link detached by hand from the Media page. <c>SetAsync</c> replaces, so
/// running it again costs a query and changes nothing.</para>
///
/// <para><b>What it cannot repair.</b> A save in which nothing changed at all: the settings library
/// writes no rows and returns before the handlers, so a no-op save never reaches this. The link is
/// therefore established the first time a logo is chosen, and re-asserted on any later real
/// change.</para>
///
/// <para>Registered with <c>.OnChanged&lt;BrandingSettings, BrandingLogoAttachment&gt;()</c>.
/// Exceptions here are logged and swallowed by the settings library, so a media failure cannot
/// stop the setting being saved.</para>
/// </summary>
public sealed class BrandingLogoAttachment(IMediaManager media) : ISettingChangedHandler<BrandingSettings>
{
    /// <summary>The owner these attachments are filed under. Two strings; no row anywhere.</summary>
    public const string OwnerEntityName = "Branding";

    /// <inheritdoc cref="OwnerEntityName"/>
    public const string OwnerEntityId = "branding";

    /// <summary>The role, declared in Program.cs as exactly one per owner.</summary>
    public const string Role = "logo";

    /// <param name="previous">Not used: the link is re-asserted from <paramref name="current"/>
    /// whether or not the id itself changed, which is what lets any real change to the group
    /// repair a hand-detached link.</param>
    public async Task OnChangedAsync(
        BrandingSettings previous,
        BrandingSettings current,
        CancellationToken cancellationToken = default)
    {
        var accessor = media.For(OwnerEntityName, OwnerEntityId);

        if (current.LogoFileId == Guid.Empty)
        {
            // Cleared: drop the link, leave the file. Another entity may well be using it.
            var held = await accessor.GetByRoleAsync(Role, cancellationToken);
            if (held is not null) await accessor.DetachAsync(held.Id, Role, cancellationToken);
            return;
        }

        // SetAsync replaces whatever held the role, so the previous logo is detached in the same
        // step — and only detached. Running it on an unchanged id is a no-op worth paying for.
        await accessor.SetAsync(Role, current.LogoFileId, cancellationToken);
    }
}
