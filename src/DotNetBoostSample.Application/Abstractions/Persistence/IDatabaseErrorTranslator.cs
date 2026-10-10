using DotNetBoostSample.Application.Abstractions.Results;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace DotNetBoostSample.Application.Abstractions.Persistence;

/// <summary>
/// Turns a constraint the database refused into an <see cref="Error"/> this API can answer with.
///
/// <para><b>Why this is an interface and not a method.</b> Recognising a unique-index violation
/// means reading a PostgreSQL <c>SqlState</c> and a constraint name off an
/// <c>Npgsql.PostgresException</c>, and the application layer does not reference Npgsql — that is
/// the one dependency rule in this solution doing real work. So the layer that knows the provider
/// classifies the failure, and the layer that knows HTTP answers it.</para>
///
/// <para><b>Why translate at all, rather than check first.</b> Uniqueness cannot be checked
/// honestly: a query saying "no such slug yet" is already stale by the time the insert runs, so
/// the index has to stay the thing that decides. That is the opposite of the missing-category
/// check in <c>CreateProduct</c>, where the handler can ask a question whose answer does not
/// change under it — and it is why these two 500s needed two different fixes.</para>
/// </summary>
public interface IDatabaseErrorTranslator
{
    /// <summary>
    /// <c>true</c> when the failure is a constraint this application has something to say about.
    /// Anything else is left alone, so a genuine fault still surfaces as one.
    /// </summary>
    bool TryTranslate(DbUpdateException exception, [NotNullWhen(true)] out Error? error);
}
