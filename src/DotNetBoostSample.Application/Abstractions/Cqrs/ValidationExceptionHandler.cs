using DotNetBoostSample.Application.Localization;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System.Text.Json;

// Aliased for the same reason ResultExtensions does it: Abstractions.Results is a namespace in
// this assembly, so the bare name `Results` binds to it rather than to ASP.NET's helper class.
using Http = Microsoft.AspNetCore.Http.Results;

namespace DotNetBoostSample.Application.Abstractions.Cqrs;

/// <summary>
/// Turns the <see cref="ValidationException"/> that <see cref="ValidationBehavior{TRequest,TResponse}"/>
/// throws into a <c>400</c> naming the fields that failed.
///
/// <para><b>Why this class has to exist.</b> It did not, for one commit, and the comment in the
/// behavior claimed ASP.NET Core would do this by itself. It does not: FluentValidation's
/// <c>ValidationException</c> is an ordinary exception as far as the framework is concerned, so
/// <c>UseExceptionHandler</c> answered <c>500 An error occurred while processing your request</c>
/// for an empty SKU. The tests did not catch it because none of them posts an invalid body —
/// a live smoke test did. <c>IExceptionHandler</c> is the hook that makes the mapping real.</para>
///
/// <para><b>And why the localizer is resolved rather than injected.</b>
/// <c>AddExceptionHandler&lt;T&gt;</c> registers this as a <b>singleton</b>, while
/// <c>IStringLocalizer&lt;ApiMessages&gt;</c> answers in the language of the request being handled
/// and so cannot be held by one. Taking it from <c>RequestServices</c> is what keeps the title in
/// the caller's language without capturing a scoped service in a singleton.</para>
///
/// <para><b>The whole response is localized, and only half of that is this class's doing.</b> The
/// title and detail come from <c>Resources/*.json</c> like every other refusal. The per-field
/// messages come from FluentValidation, which ships translations for its own built-in rules and
/// resolves them through <c>CultureInfo.CurrentUICulture</c> — and <c>UseMultiLingual()</c> sets
/// that culture from the language it just resolved, so <c>'Sku' must not be empty.</c> arrives as
/// <c>'Sku' لا يجب أن يكون فارغاً.</c> on an Arabic request without this application doing
/// anything. Two localizers, two mechanisms, one language, because the middleware sets both.</para>
///
/// <para>The exception is a rule carrying its own <c>.WithMessage(…)</c> — the hex-colour message
/// in <c>BrandingSettingsValidator</c> is a C# literal and stays in the language it was written
/// in. That is the case to watch when adding rules, not the built-in ones.</para>
/// </summary>
public sealed class ValidationExceptionHandler : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Anything else is not ours: returning false lets the next handler, and finally the
        // framework's own 500, deal with it.
        if (exception is not ValidationException validation) return false;

        var messages = httpContext.RequestServices.GetRequiredService<IStringLocalizer<ApiMessages>>();

        // Camel-cased, so a field names itself the way the caller sent it. Grouped, because one
        // property can fail several rules at once.
        var errors = validation.Errors
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => JsonNamingPolicy.CamelCase.ConvertName(group.Key),
                group => group.Select(failure => failure.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

        await Http
            .ValidationProblem(
                errors,
                detail: messages["Validation.Detail"].Value,
                title: messages["Validation.Title"].Value)
            .ExecuteAsync(httpContext);

        return true;
    }
}
