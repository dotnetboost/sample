using DotNetBoostSample.Application.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

// Aliased because this file's own namespace ends in `Results`, so the bare name binds to the
// namespace rather than to ASP.NET's static helper class.
using Http = Microsoft.AspNetCore.Http.Results;

namespace DotNetBoostSample.Application.Abstractions.Results;

/// <summary>
/// Turns a handler's <see cref="Result{T}"/> into an HTTP response, localizing the refusal on the
/// way out.
///
/// <para><b>This is the method that replaced two helpers and eleven call sites.</b>
/// <c>CatalogEndpoints</c> carried a private <c>NotFound(messages, key, id)</c> and a private
/// <c>Wrote(messages, key, subject)</c>, and every handler in the file took
/// <see cref="IStringLocalizer{ApiMessages}"/> in order to call them. The shape they produced is
/// unchanged here — RFC 7807 problem details, so a refusal from this application looks like one
/// from any of the three libraries and the dashboard needs no special case — but the rule now
/// lives once.</para>
///
/// <para><b>Why the status code is derived and not passed.</b> A handler knows what went wrong;
/// it should not also be choosing between 404 and 409. <see cref="ErrorKind"/> is that decision
/// made once, at the only place that knows the answer is going over HTTP at all.</para>
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// <paramref name="onSuccess"/> shapes the 200 — a created id, or the localized sentence a
    /// write answers with. Left out, the value is returned as-is, which is what a read wants.
    /// </summary>
    public static IResult ToHttpResult<T>(
        this Result<T> result,
        IStringLocalizer<ApiMessages> messages,
        Func<T, IResult>? onSuccess = null) =>
        result.IsSuccess
            ? onSuccess?.Invoke(result.Value!) ?? Http.Ok(result.Value)
            : Http.Problem(
                detail: messages[result.Error!.MessageKey, result.Error.Arguments],
                statusCode: result.Error.Kind switch
                {
                    ErrorKind.NotFound => StatusCodes.Status404NotFound,
                    ErrorKind.Conflict => StatusCodes.Status409Conflict,

                    // Unreachable while ErrorKind has two members, and a 500 rather than a
                    // plausible 400 if a third is ever added without coming back here.
                    _ => StatusCodes.Status500InternalServerError
                });
}
