using DotNetBoost.Auditing.Core.Interfaces;
using DotNetBoost.Auditing.Core.Services;
using DotNetBoostSample.Application.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetBoostSample.Application.Composition;

/// <summary>
/// Records that an entity was created, in a unit of work of its own — the one entry this
/// application writes that is <em>not</em> committed with the change it describes, and the sixth
/// place where two of these libraries have to be told about each other.
///
/// <para><b>Why the catalogue records its own changes at all.</b> DotNetBoost.Auditing normally
/// needs no help: <c>UseAuditing(sp)</c> attaches a <c>SaveChangesInterceptor</c>, it reads the
/// change tracker, and no handler mentions auditing ever again. That interceptor cannot be attached
/// to this application's context — <c>Translation</c> has a composite key an audit entry cannot
/// address — so the six write slices record explicitly instead.
/// <c>Infrastructure/DependencyInjection.cs</c> has that finding in full. Five of the six are one
/// line and cost nothing: the EF Core store <em>tracks</em> an entry rather than writing it, so an
/// update or a delete recorded before the save rides along in it.</para>
///
/// <para><b>Why a creation is the one that cannot.</b> An entry is addressed by the entity's key,
/// and a store-generated key does not exist until the <c>INSERT</c> has run. So a creation can only
/// be recorded afterwards — which is exactly the case the library's own interceptor defers to a
/// second pass.</para>
///
/// <para><b>And why that second save cannot be this request's.</b> The obvious shape —
/// <c>await audit.For(product).CreatedAsync(…); await db.SaveChangesAsync(ct);</c> — is wrong here,
/// and wrong in a way that costs data rather than a test: a second <c>SaveChanges</c> on a context
/// still tracking a <c>[MultiLingual]</c> entity writes that entity's <em>in-memory</em> text over
/// the translations the first save stored. For a product created through <c>TranslatedText</c>
/// those properties were never assigned — the text went to translation rows, not to the instance —
/// so the second save stores empty values for the request's language and the name reads back as
/// <c>""</c>. Two integration tests caught it, which is how it was found.</para>
///
/// <para>So the entry goes in a scope of its own: a second <see cref="IAppDbContext"/>, tracking
/// nothing but the entry and saving only it. The <c>[MultiLingual]</c> entity is not in that
/// context, so there is nothing for MultiLingual's interceptor to rewrite.</para>
///
/// <para><b>What that costs, plainly.</b> A creation and its trail are in two transactions. If the
/// first commits and this does not, the row exists and its creation is not recorded — the window
/// the library closes for its own interceptor by owning a transaction around both passes, which a
/// handler holding <see cref="IAppDbContext"/> has no way to open. The honest fix is not more code
/// here: it is keys assigned in the application rather than by the database, which would put a
/// creation back on the single-save path with everything else.</para>
///
/// <para>Scoped, like the other composition services, and injected into the two create slices so
/// that neither of them has to know any of the above — only that recording a creation is a call
/// rather than a save.</para>
/// </summary>
public sealed class CreationTrail(IServiceScopeFactory scopes)
{
    /// <summary>
    /// Records <paramref name="entity"/>'s creation, with its initial values.
    /// </summary>
    /// <param name="entity">
    /// The entity as created, <b>after</b> the save that assigned its key. It is read, never
    /// tracked: the audit engine takes the entity name, the key and the type off it, and
    /// <see cref="AuditDiff.InitialValues"/> reads its properties — minus the ones carrying
    /// <c>[AuditIgnore]</c>, which the engine drops.
    /// </param>
    /// <param name="ct">The request's token.</param>
    public async Task RecordAsync(object entity, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();

        var audit = scope.ServiceProvider.GetRequiredService<IAuditManager>();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        // Tracked by the new scope's context rather than written, like every other entry this
        // application records — so this save is what makes it durable, and it is the only thing in
        // that context to save.
        await audit.For(entity).CreatedAsync(AuditDiff.InitialValues(entity), ct: ct);

        await db.SaveChangesAsync(ct);
    }
}
