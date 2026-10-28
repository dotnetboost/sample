using DotNetBoostSample.Application.Abstractions.Results;
using DotNetBoostSample.Application.Localization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace DotNetBoostSample.Application.Abstractions.Persistence;

/// <summary>
/// Answers a refused constraint with the status code it deserves, rather than with a 500.
///
/// <para><b>What this closes.</b> Creating a category whose slug is already taken, or a product
/// whose SKU is, reached the caller as <c>500 An error occurred while processing your request</c>.
/// The unique index was doing exactly what it is for; nothing had decided what its refusal should
/// look like over HTTP. It also covers the race the <c>CreateProduct</c> handler documents and
/// deliberately does not close — a category deleted between the check and the insert — which was
/// the last 500 on a request a caller could actually provoke.</para>
///
/// <para><b>Why it is an exception handler and not a catch in each slice.</b> Four writes can
/// violate one of these constraints, and a <c>try</c> around each <c>SaveChangesAsync</c> is four
/// places to keep in step — plus the seeder and anything added later. Registered once, this one
/// covers every write there is, including writes made by the four libraries' own endpoints.</para>
///
/// <para>The classification is <see cref="IDatabaseErrorTranslator"/>'s, implemented in the
/// infrastructure layer where Npgsql is referenced. If it declines, so does this: a
/// <c>DbUpdateException</c> that is not a constraint anybody anticipated is a real fault and
/// should still read like one.</para>
/// </summary>
public sealed class DbUpdateExceptionHandler : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateException update) return false;

        // Resolved per request for the reason ValidationExceptionHandler gives: AddExceptionHandler
        // registers a singleton, and both of these are scoped.
        var services = httpContext.RequestServices;

        if (!services.GetRequiredService<IDatabaseErrorTranslator>().TryTranslate(update, out var error))
            return false;

        var messages = services.GetRequiredService<IStringLocalizer<ApiMessages>>();

        await error.ToProblem(messages).ExecuteAsync(httpContext);

        return true;
    }
}
