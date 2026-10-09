namespace DotNetBoostSample.Application.Abstractions.Results;

/// <summary>
/// What kind of refusal this is — and nothing about HTTP, which is
/// <see cref="ResultExtensions"/>'s job.
/// </summary>
public enum ErrorKind
{
    /// <summary>The thing the id named does not exist.</summary>
    NotFound,

    /// <summary>It exists, and the state it is in forbids this.</summary>
    Conflict
}

/// <summary>
/// A refusal, as a message KEY and its arguments rather than as a sentence.
///
/// <para><b>Why a key.</b> Every refusal this application makes is localized — it answers in the
/// language the content in the same response was translated into, which is the point
/// <c>ApiMessages</c> exists to make. A handler that formatted the sentence itself would need
/// <c>IStringLocalizer&lt;ApiMessages&gt;</c> injected into it, which is what the old
/// <c>CatalogEndpoints</c> did in all eleven handlers. Carrying the key instead moves the
/// localizer to the one place that turns a result into a response.</para>
/// </summary>
/// <param name="Kind">Which refusal, which decides the status code.</param>
/// <param name="MessageKey">A key in <c>Resources/{en,ar,fr}.json</c>.</param>
/// <param name="Arguments">Whatever that message formats — an id, a slug, a SKU.</param>
public sealed record Error(ErrorKind Kind, string MessageKey, object[] Arguments)
{
    /// <summary>The id named nothing.</summary>
    public static Error NotFound(string messageKey, params object[] arguments) =>
        new(ErrorKind.NotFound, messageKey, arguments);

    /// <summary>It exists, and this is not allowed yet.</summary>
    public static Error Conflict(string messageKey, params object[] arguments) =>
        new(ErrorKind.Conflict, messageKey, arguments);
}

/// <summary>
/// A handler's answer: a value, or an <see cref="Error"/>.
///
/// <para>Only the slices that can refuse use it. <c>ListProducts</c> returns
/// <c>PagedResult&lt;T&gt;</c> directly, because an empty catalogue is an empty page and not a
/// failure — wrapping every response in a result type to be uniform would add a layer that is
/// unwrapped unconditionally in half the endpoints.</para>
///
/// <para>The two implicit conversions are what keep a handler reading like the code it replaced:
/// <c>return Error.NotFound(…)</c> and <c>return slug;</c>, with no <c>Result.Ok</c> ceremony at
/// either end.</para>
/// </summary>
public sealed class Result<T>
{
    private Result(T value) => Value = value;

    private Result(Error error) => Error = error;

    /// <summary>The value, when <see cref="IsSuccess"/>.</summary>
    public T? Value { get; }

    /// <summary>The refusal, when not.</summary>
    public Error? Error { get; }

    /// <summary>Whether this carries a value.</summary>
    public bool IsSuccess => Error is null;

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);
}
