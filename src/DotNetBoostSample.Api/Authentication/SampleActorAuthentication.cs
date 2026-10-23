using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DotNetBoostSample.Api.Authentication;

/// <summary>
/// A stand-in for authentication, so that this sample can show DotNetBoost.Auditing's endpoints
/// behind the authorization they require, and record a real name on every change.
///
/// <para><b>This is not authentication.</b> It trusts a request header. Anyone who can reach the
/// API can claim to be anyone, and nothing here verifies a password, a token or a signature.
/// Deleting this file and registering your real scheme is the whole of the change — the audit
/// engine only ever asks <c>HttpContext.User</c> who is acting, so it is already compatible with
/// whatever answers that.</para>
///
/// <para><b>Why it exists at all, in a sample that had no authentication.</b> Two of the library's
/// own decisions insisted on it.</para>
///
/// <para>The first: <c>MapAuditEndpoints()</c> requires an authenticated caller <em>by
/// default</em>, which the three sibling libraries do not do for their own endpoints. That is not
/// an oversight to work around with <c>RequireAuthorization = false</c> — a trail says who did
/// what and when, which is a map of an organisation's activity and, through the actor, personal
/// data about its staff. The quick start in the library's README turns it off and says <b>DO NOT
/// COPY THIS LINE</b>. So this sample does not copy it: the endpoints are mapped exactly as the
/// library maps them, and something had to be able to sign in.</para>
///
/// <para>The second: without a signed-in user, <c>UseHttpContextActor()</c> has nothing to read
/// and every entry's actor is null. A trail that answers "what changed" and "unknown" to "who
/// changed it" demonstrates half a library. A header is a poor authenticator and a perfectly good
/// <em>actor</em>, and the actor is the part this sample is showing.</para>
///
/// <para>Everything else stays anonymous. Only the audit endpoints require a caller, because only
/// they ask for one; the catalogue, the settings, the media and the languages are reachable as
/// before. What this scheme adds to those is attribution: a write that arrives with the header
/// gets recorded against that name, and one that does not is recorded with no name rather than
/// with a guess.</para>
/// </summary>
public sealed class SampleActorAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>The scheme's name, in one place, since the host registers it and the tests use it.</summary>
    public const string SchemeName = "SampleActor";

    /// <summary>
    /// The header that names the caller — <c>X-Sample-Actor: alice@example.com</c>.
    ///
    /// <para>Prefixed <c>X-Sample-</c> rather than borrowing <c>Authorization</c>, so that nothing
    /// about it can be mistaken for a credential, by a reader or by a proxy.</para>
    /// </summary>
    public const string ActorHeader = "X-Sample-Actor";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var actor = Request.Headers[ActorHeader].ToString().Trim();

        // NoResult rather than Fail: no header is an anonymous request, which is allowed
        // everywhere except the audit endpoints. Fail would turn every unauthenticated read of
        // the catalogue into a 401.
        if (string.IsNullOrEmpty(actor)) return Task.FromResult(AuthenticateResult.NoResult());

        // ClaimTypes.Name, because that is what IIdentity.Name reads and what the library's
        // HttpContextAuditActorAccessor therefore records. The engine caps the stored actor at
        // AuditingOptions.ActorMaxLength itself, so a long name is truncated in the trail rather
        // than refused here — the sample does not need a second opinion about the limit.
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, actor)], SchemeName);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
