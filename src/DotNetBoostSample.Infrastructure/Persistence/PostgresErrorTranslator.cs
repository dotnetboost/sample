using DotNetBoostSample.Application.Abstractions.Persistence;
using DotNetBoostSample.Application.Abstractions.Results;
using DotNetBoostSample.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Diagnostics.CodeAnalysis;

namespace DotNetBoostSample.Infrastructure.Persistence;

/// <summary>
/// Recognises the two constraint violations this application has something to say about, and
/// names the field where it can.
///
/// <para><b>Why this class is in the infrastructure layer.</b> It is the only file in the solution
/// that reads <c>Npgsql.PostgresException</c> — the <c>SqlState</c> and the constraint name are
/// PostgreSQL's vocabulary, not the application's. Swapping provider replaces this one class and
/// touches nothing else: the <see cref="Error"/> it answers with, and the HTTP status that follows
/// from it, are decided elsewhere.</para>
///
/// <para><b>And why it belongs beside the configurations.</b> The constraint names it matches are
/// the ones <see cref="CategoryConfiguration"/> and <see cref="ProductConfiguration"/> create, as
/// constants, so the index and the sentence about it are one edit apart and the compiler keeps
/// them together.</para>
///
/// <para><b>What it deliberately does not do.</b> It does not name the offending VALUE. Postgres
/// puts it in <c>Detail</c> — <c>Key (Slug)=(furniture) already exists.</c> — as prose, in the
/// server's language, and only when <c>Include Error Detail</c> is on. Parsing that to recover
/// "furniture" would be a third-party format masquerading as an API contract. The difference from
/// <c>CreateProduct</c>'s missing-category refusal, which does name the id, is that there the
/// handler already had it in hand.</para>
/// </summary>
public sealed class PostgresErrorTranslator : IDatabaseErrorTranslator
{
    /// <inheritdoc/>
    public bool TryTranslate(DbUpdateException exception, [NotNullWhen(true)] out Error? error)
    {
        ArgumentNullException.ThrowIfNull(exception);

        error = exception.InnerException switch
        {
            // Two things claiming one slug or one SKU. A conflict rather than a bad request: the
            // body is well formed, and it is the state of the table that refuses it.
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } unique =>
                unique.ConstraintName switch
                {
                    CategoryConfiguration.SlugIndex => Error.Conflict("Catalog.SlugTaken"),
                    ProductConfiguration.SkuIndex   => Error.Conflict("Catalog.SkuTaken"),

                    // Some other unique index — one of the three libraries' own, or one added
                    // without coming back here. Still a 409 and still not a 500, because the
                    // shape of the answer does not depend on knowing which field it was.
                    _ => Error.Conflict("Database.ValueTaken")
                },

            // A row pointed at something that is not there. CreateProduct and UpdateProduct check
            // for the common case and say which category — this is the race their comments admit
            // to: the category was deleted between that check and this insert.
            PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } =>
                Error.Conflict("Database.RelatedRowMissing"),

            // Anything else is a real fault and should read like one.
            _ => null
        };

        return error is not null;
    }
}
