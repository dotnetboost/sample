using FluentValidation;

namespace DotNetBoostSample.Application.Abstractions.Cqrs;

/// <summary>
/// Runs every <see cref="IValidator{T}"/> registered for a request before its handler sees it.
///
/// <para><b>What this moves.</b> The catalogue used to clamp: <c>Math.Clamp(pageSize ??
/// catalog.PageSize, 1, MaxPageSize)</c> and <c>Math.Max(page ?? 1, 1)</c>, so asking for page 0
/// quietly served page 1 and asking for 5,000 rows quietly served 200. Clamping is the right
/// behaviour for a value read from the SETTINGS store — a stored page size is applied, not
/// refused — but a caller's own parameter is a request, and a request that cannot be honoured is
/// better refused than silently rewritten. The bounds now sit in each slice's
/// <c>Validator</c>, and this is what runs them.</para>
///
/// <para>The failure is thrown rather than returned, because ASP.NET Core's problem-details
/// handler already turns a <see cref="ValidationException"/> into a 400 carrying the failures.
/// <c>Result&lt;T&gt;</c> is for refusals the application has something to SAY about — a missing
/// category, a category that still has products — and those are localized. A page index below one
/// needs no sentence from the resource files.</para>
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <inheritdoc/>
    public async Task<TResponse> HandleAsync(
        TRequest request, Func<Task<TResponse>> next, CancellationToken ct)
    {
        // Almost every request has none, and the loop costs nothing when the enumerable is empty
        // — which is why this is registered once for every pair rather than per slice.
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, ct);

            if (!result.IsValid) throw new ValidationException(result.Errors);
        }

        return await next();
    }
}
